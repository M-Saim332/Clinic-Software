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
        purchase.Items = conn.Query<PurchaseItem>(@"SELECT
                ROW_NUMBER() OVER (ORDER BY pi.PurchaseItemID) SerialNumber,
                pi.*,
                prod.Name ProductName
            FROM PurchaseItems pi
            JOIN Products prod ON pi.ProductID=prod.ProductID
            WHERE pi.PurchaseID=@id
            ORDER BY pi.PurchaseItemID", new { id }).ToList();
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

    /// <summary>
    /// Creates (or updates a legacy unposted draft) and posts a purchase in one database transaction.
    /// Review screens never call this method; it is reserved for the final confirmation action.
    /// </summary>
    public int SaveAndPost(Purchase purchase)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

        int purchaseId;
        if (purchase.PurchaseID > 0)
        {
            var posted = conn.ExecuteScalar<bool?>("SELECT IsPosted FROM Purchases WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseID=@PurchaseID", purchase, tx);
            if (!posted.HasValue) throw new InvalidOperationException("Purchase was not found.");
            if (posted.Value) throw new InvalidOperationException("Purchase invoice is already posted.");

            purchaseId = purchase.PurchaseID;
            conn.Execute(@"UPDATE Purchases SET InvoiceNumber=@InvoiceNumber,PurchaseDate=@PurchaseDate,SupplierID=@SupplierID,
                SupplierName=@SupplierName,TotalAmount=@TotalAmount,CreatedBy=@CreatedBy,CreatedByName=@CreatedByName
                WHERE PurchaseID=@PurchaseID", purchase, tx);
            conn.Execute("DELETE FROM PurchaseItems WHERE PurchaseID=@purchaseId", new { purchaseId }, tx);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(purchase.InvoiceNumber) || purchase.InvoiceNumber == "Auto-generated")
            {
                var next = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PurchaseID),0)+1 FROM Purchases WITH (UPDLOCK,HOLDLOCK)", transaction: tx);
                purchase.InvoiceNumber = $"PUR-{next:D6}";
            }

            purchaseId = conn.ExecuteScalar<int>(@"INSERT INTO Purchases
                (InvoiceNumber,PurchaseDate,SupplierID,SupplierName,TotalAmount,CreatedBy,CreatedByName,IsPosted,PostedAt)
                VALUES (@InvoiceNumber,@PurchaseDate,@SupplierID,@SupplierName,@TotalAmount,@CreatedBy,@CreatedByName,0,NULL);
                SELECT CONVERT(INT,SCOPE_IDENTITY());", purchase, tx);
        }

        ReplaceItems(conn, tx, purchaseId, purchase.Items);
        PostPurchase(conn, tx, purchaseId);
        tx.Commit();
        return purchaseId;
    }

    private static void ReplaceItems(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int purchaseId, IEnumerable<PurchaseItem> items)
    {
        foreach (var item in items)
        {
            if (!item.ExpiryDate.HasValue) throw new InvalidOperationException($"Expiry date is required for {item.ProductName ?? "each product"}.");
            item.PurchaseID = purchaseId;
            item.PackageQuantity = item.PackageQuantity > 0 ? item.PackageQuantity : item.Quantity;
            item.Quantity = item.PackageQuantity + Math.Max(0, item.BonusQuantity);
            item.UnitsPerPackage = conn.ExecuteScalar<int>(
                "SELECT CASE WHEN ISNULL(PiecesPerUnit, 0) <= 0 THEN 1 ELSE PiecesPerUnit END FROM Products WHERE ProductID=@ProductID",
                new { item.ProductID }, tx);
            conn.Execute(@"INSERT INTO PurchaseItems
                (PurchaseID,ProductID,BatchNumber,ExpiryDate,Quantity,BonusQuantity,PackageQuantity,UnitsPerPackage,PurchasePrice,PackMRP,Discount,Tax,ExtraDiscount,ATax,CompanySalesTax)
                VALUES (@PurchaseID,@ProductID,@BatchNumber,@ExpiryDate,@Quantity,@BonusQuantity,@PackageQuantity,@UnitsPerPackage,@PurchasePrice,@PackMRP,@Discount,@Tax,@ExtraDiscount,@ATax,@CompanySalesTax)", item, tx);
        }
    }

    public void PostPurchase(int purchaseId)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        PostPurchase(conn, tx, purchaseId);
        tx.Commit();
    }

    private static void PostPurchase(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int purchaseId)
    {
        var purchase = conn.QuerySingleOrDefault<Purchase>("SELECT * FROM Purchases WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseID=@purchaseId", new { purchaseId }, tx)
            ?? throw new InvalidOperationException("Purchase was not found.");
        if (purchase.IsPosted) return;
        var items = conn.Query<PurchaseItem>("SELECT * FROM PurchaseItems WHERE PurchaseID=@purchaseId", new { purchaseId }, tx).ToList();
        if (items.Count == 0) throw new InvalidOperationException("A purchase must contain at least one item.");
        foreach (var item in items)
        {
            var piecesPerUnit = conn.ExecuteScalar<int>(
                "SELECT CASE WHEN ISNULL(PiecesPerUnit, 0) <= 0 THEN 1 ELSE PiecesPerUnit END FROM Products WHERE ProductID=@ProductID",
                new { item.ProductID }, tx);
            var stockQuantity = (item.PackageQuantity + item.BonusQuantity) * piecesPerUnit;
            var effectiveCostPerPiece = item.EffectiveCostPerPiece;
            var packPurchasePrice = effectiveCostPerPiece * piecesPerUnit;
            // ProductStock stores the trade price entered for this specific expiry batch.
            var batchPurchasePrice = item.PurchasePrice;
            conn.Execute(@"UPDATE Products SET 
                PurchasePrice=@EffectiveCostPerPiece,
                Rate=@PackPurchasePrice,
                SellingPrice=CASE WHEN @PackMRP > 0 THEN @PackMRP ELSE SellingPrice END,
                LastStockUpdateDate=CAST(GETDATE() AS DATE) 
                WHERE ProductID=@ProductID AND IsActive=1",
                new { 
                    EffectiveCostPerPiece=effectiveCostPerPiece, 
                    PackPurchasePrice=packPurchasePrice,
                    PackMRP=item.PackMRP, 
                    item.ProductID 
                }, tx);
                
            var stockId = conn.ExecuteScalar<int?>(@"SELECT StockID FROM ProductStock WITH (UPDLOCK,HOLDLOCK) WHERE ProductID=@ProductID AND ExpiryDate=CAST(@ExpiryDate AS DATE)", new { item.ProductID, item.ExpiryDate }, tx);
            if (stockId.HasValue)
            {
                conn.Execute(@"UPDATE ProductStock SET QuantityAvailable = QuantityAvailable + @Quantity, PurchasePrice = @BatchPurchasePrice, MRP = @PackMRP, IsArchived = 0 WHERE StockID=@StockID",
                    new { Quantity = stockQuantity, BatchPurchasePrice = batchPurchasePrice, PackMRP = item.PackMRP, StockID = stockId.Value }, tx);
            }
            else
            {
                conn.Execute(@"INSERT INTO ProductStock (ProductID, ExpiryDate, QuantityAvailable, PurchasePrice, MRP) VALUES (@ProductID, CAST(@ExpiryDate AS DATE), @Quantity, @BatchPurchasePrice, @PackMRP)", 
                    new { item.ProductID, item.ExpiryDate, Quantity = stockQuantity, BatchPurchasePrice = batchPurchasePrice, PackMRP = item.PackMRP }, tx);
            }
        }
        conn.Execute("UPDATE Purchases SET IsPosted=1,PostedAt=SYSDATETIME() WHERE PurchaseID=@purchaseId", new { purchaseId }, tx);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("DELETE FROM Purchases WHERE PurchaseID=@id AND IsPosted=0", new { id }) == 1;
    }
}
