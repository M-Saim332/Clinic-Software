using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class PurchaseRepository
{
    private readonly DatabaseSession _session;
    public PurchaseRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Purchase> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Purchase>(@"SELECT p.*,COALESCE(s.Name,p.SupplierName) SupplierName,
            COALESCE(u.FullName,p.CreatedByName) CreatedByName FROM Purchases p
            LEFT JOIN Suppliers s ON p.SupplierID=s.SupplierID LEFT JOIN Users u ON p.CreatedBy=u.UserID
            ORDER BY p.PurchaseDate DESC,p.PurchaseID DESC");
    }

    public decimal GetTodayTotal()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM(TotalAmount),0) FROM Purchases WHERE IsPosted=1 AND CAST(PurchaseDate AS DATE)=CAST(GETDATE() AS DATE)");
    }

    public decimal GetTotalByRange(DateTime fromInclusive, DateTime toExclusive)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM(TotalAmount),0) FROM Purchases WHERE IsPosted=1 AND PurchaseDate>=@fromInclusive AND PurchaseDate<@toExclusive", new { fromInclusive, toExclusive });
    }

    public IEnumerable<(DateTime Date, decimal Total)> GetDailyTotalsLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(@"SELECT CAST(PurchaseDate AS DATE) PurchaseDay,ISNULL(SUM(TotalAmount),0) Total
            FROM Purchases WHERE IsPosted=1 AND PurchaseDate>=DATEADD(day,-29,CAST(GETDATE() AS DATE))
            GROUP BY CAST(PurchaseDate AS DATE) ORDER BY PurchaseDay");
        return rows.Select(r => ((DateTime)r.PurchaseDay, (decimal)r.Total)).ToList();
    }

    public Purchase? GetByIdWithItems(int id)
    {
        using var conn = _session.CreateConnection();
        var purchase = conn.QuerySingleOrDefault<Purchase>(@"SELECT p.*,COALESCE(s.Name,p.SupplierName) SupplierName,
            COALESCE(u.FullName,p.CreatedByName) CreatedByName FROM Purchases p
            LEFT JOIN Suppliers s ON p.SupplierID=s.SupplierID LEFT JOIN Users u ON p.CreatedBy=u.UserID
            WHERE p.PurchaseID=@id", new { id });
        if (purchase == null) return null;
        purchase.Items = conn.Query<PurchaseItem>(@"SELECT pi.*,prod.Name ProductName FROM PurchaseItems pi
            JOIN Products prod ON pi.ProductID=prod.ProductID WHERE pi.PurchaseID=@id", new { id }).ToList();
        return purchase;
    }

    public string GetNextInvoiceNumber()
    {
        using var conn = _session.CreateConnection();
        var next = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PurchaseID),0)+1 FROM Purchases");
        return $"PUR-{next:D6}";
    }

    public int Insert(Purchase purchase)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        if (string.IsNullOrWhiteSpace(purchase.InvoiceNumber) || purchase.InvoiceNumber == "Auto-generated")
        {
            var next = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PurchaseID),0)+1 FROM Purchases WITH (UPDLOCK,HOLDLOCK)", transaction: tx);
            purchase.InvoiceNumber = $"PUR-{next:D6}";
        }
        purchase.IsPosted = false;
        var id = conn.ExecuteScalar<int>(@"INSERT INTO Purchases
            (InvoiceNumber,PurchaseDate,SupplierID,SupplierName,TotalAmount,CreatedBy,CreatedByName,IsPosted,PostedAt)
            VALUES (@InvoiceNumber,@PurchaseDate,@SupplierID,@SupplierName,@TotalAmount,@CreatedBy,@CreatedByName,0,NULL);
            SELECT CONVERT(INT,SCOPE_IDENTITY());", purchase, tx);
        ReplaceItems(conn, tx, id, purchase.Items);
        tx.Commit();
        return id;
    }

    public void Update(Purchase purchase)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        var posted = conn.ExecuteScalar<bool?>("SELECT IsPosted FROM Purchases WHERE PurchaseID=@PurchaseID", purchase, tx);
        if (!posted.HasValue) throw new InvalidOperationException("Purchase was not found.");
        if (posted.Value) throw new InvalidOperationException("Posted purchases cannot be edited.");
        conn.Execute(@"UPDATE Purchases SET PurchaseDate=@PurchaseDate,SupplierID=@SupplierID,SupplierName=@SupplierName,
            TotalAmount=@TotalAmount,CreatedBy=@CreatedBy,CreatedByName=@CreatedByName WHERE PurchaseID=@PurchaseID", purchase, tx);
        conn.Execute("DELETE FROM PurchaseItems WHERE PurchaseID=@PurchaseID", purchase, tx);
        ReplaceItems(conn, tx, purchase.PurchaseID, purchase.Items);
        tx.Commit();
    }

    private static void ReplaceItems(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int purchaseId, IEnumerable<PurchaseItem> items)
    {
        foreach (var item in items)
        {
            if (!item.ExpiryDate.HasValue) throw new InvalidOperationException($"Expiry date is required for {item.ProductName ?? "each product"}.");
            item.PurchaseID = purchaseId;
            item.PackageQuantity = item.PackageQuantity > 0 ? item.PackageQuantity : item.Quantity;
            item.Quantity = item.PackageQuantity + Math.Max(0, item.BonusQuantity);
            conn.Execute(@"INSERT INTO PurchaseItems
                (PurchaseID,ProductID,BatchNumber,ExpiryDate,Quantity,BonusQuantity,PackageQuantity,PurchasePrice,PackMRP,Discount,Tax,ExtraDiscount,ATax)
                VALUES (@PurchaseID,@ProductID,@BatchNumber,@ExpiryDate,@Quantity,@BonusQuantity,@PackageQuantity,@PurchasePrice,@PackMRP,@Discount,@Tax,@ExtraDiscount,@ATax)", item, tx);
        }
    }

    public void PostPurchase(int purchaseId)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var purchase = conn.QuerySingleOrDefault<Purchase>("SELECT * FROM Purchases WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseID=@purchaseId", new { purchaseId }, tx)
            ?? throw new InvalidOperationException("Purchase was not found.");
        if (purchase.IsPosted) { tx.Commit(); return; }
        var items = conn.Query<PurchaseItem>("SELECT * FROM PurchaseItems WHERE PurchaseID=@purchaseId", new { purchaseId }, tx).ToList();
        if (items.Count == 0) throw new InvalidOperationException("A purchase must contain at least one item.");
        foreach (var item in items)
        {
            var piecesPerUnit = Math.Max(1, conn.ExecuteScalar<int>("SELECT ISNULL(NULLIF(UnitsPerPackage,0),1) FROM PurchaseItems WHERE PurchaseItemID = (SELECT MAX(PurchaseItemID) FROM PurchaseItems WHERE PurchaseID=@purchaseId AND ProductID=@ProductID)", new { purchaseId, item.ProductID }, tx));
            // fallback to stored PiecesPerUnit in item
            if (item.UnitsPerPackage > 0) piecesPerUnit = item.UnitsPerPackage;
            var stockQuantity = (item.PackageQuantity + item.BonusQuantity) * Math.Max(1, piecesPerUnit);
            // Update stock, cost price, Pack MRP, and PiecesPerUnit so clinic price menu reflects latest batch
            conn.Execute(@"UPDATE Products SET 
                Stock=Stock+@Quantity,
                PurchasePrice=@EffectiveRate,
                SellingPrice=@PackMRP,
                PiecesPerUnit=@PiecesPerUnit,
                LastStockUpdateDate=CAST(GETDATE() AS DATE) 
                WHERE ProductID=@ProductID AND IsActive=1",
                new { Quantity=stockQuantity, EffectiveRate=item.EffectiveRate, PackMRP=item.PackMRP, PiecesPerUnit=piecesPerUnit, item.ProductID }, tx);
        }
        conn.Execute("UPDATE Purchases SET IsPosted=1,PostedAt=SYSDATETIME() WHERE PurchaseID=@purchaseId", new { purchaseId }, tx);
        tx.Commit();
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("DELETE FROM Purchases WHERE PurchaseID=@id AND IsPosted=0", new { id }) == 1;
    }
}
