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
        return conn.Query<Purchase>(
            @"SELECT p.*, COALESCE(s.Name, p.SupplierName) AS SupplierName,
                     COALESCE(u.FullName, p.CreatedByName) AS CreatedByName
              FROM Purchases p
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              LEFT JOIN Users u ON p.CreatedBy = u.UserID
              ORDER BY p.PurchaseDate DESC");
    }

    public decimal GetTodayTotal()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(TotalAmount), 0) FROM Purchases WHERE CAST(PurchaseDate AS DATE) = CAST(GETDATE() AS DATE)");
    }

    public decimal GetTotalByRange(DateTime fromInclusive, DateTime toExclusive)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM(TotalAmount), 0) FROM Purchases WHERE PurchaseDate >= @fromInclusive AND PurchaseDate < @toExclusive", new { fromInclusive, toExclusive });
    }

    /// <summary>Returns daily purchase totals for the last 30 days for the profit chart.</summary>
    public IEnumerable<(DateTime Date, decimal Total)> GetDailyTotalsLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(
            @"SELECT CAST(PurchaseDate AS DATE) AS PurchaseDay,
                     ISNULL(SUM(TotalAmount), 0) AS Total
              FROM Purchases
              WHERE PurchaseDate >= DATEADD(day, -29, CAST(GETDATE() AS DATE))
              GROUP BY CAST(PurchaseDate AS DATE)
              ORDER BY PurchaseDay");
        return rows.Select(r => ((DateTime)r.PurchaseDay, (decimal)r.Total)).ToList();
    }


    public Purchase? GetByIdWithItems(int id)
    {
        using var conn = _session.CreateConnection();
        var purchase = conn.QuerySingleOrDefault<Purchase>(
            @"SELECT p.*, COALESCE(s.Name, p.SupplierName) AS SupplierName,
                     COALESCE(u.FullName, p.CreatedByName) AS CreatedByName
              FROM Purchases p
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              LEFT JOIN Users u ON p.CreatedBy = u.UserID
              WHERE p.PurchaseID = @id", new { id });

        if (purchase == null) return null;

        purchase.Items = conn.Query<PurchaseItem>(
            @"SELECT pi.*, prod.Name AS ProductName
              FROM PurchaseItems pi
              JOIN Products prod ON pi.ProductID = prod.ProductID
              WHERE pi.PurchaseID = @id", new { id }).ToList();

        return purchase;
    }

    public int Insert(Purchase p)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (string.IsNullOrEmpty(p.InvoiceNumber) || p.InvoiceNumber == "Auto-generated")
            {
                string datePrefix = p.PurchaseDate.ToString("yyyyMMdd");
                int nextSeq = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Purchases WHERE CONVERT(DATE, PurchaseDate) = @date", 
                    new { date = p.PurchaseDate.Date }, tx) + 1;
                p.InvoiceNumber = $"PUR-{datePrefix}-{nextSeq:D3}";
            }

            var purchaseId = conn.ExecuteScalar<int>(
                @"INSERT INTO Purchases (InvoiceNumber, PurchaseDate, SupplierID, SupplierName, TotalAmount, CreatedBy, CreatedByName)
                  VALUES (@InvoiceNumber, @PurchaseDate, @SupplierID, @SupplierName, @TotalAmount, @CreatedBy, @CreatedByName);
                  SELECT SCOPE_IDENTITY();", p, tx);

            ProcessPurchaseItems(conn, tx, purchaseId, p.Items);

            tx.Commit();
            return purchaseId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Purchase p)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Revert old stock
            var oldItems = conn.Query<PurchaseItem>("SELECT * FROM PurchaseItems WHERE PurchaseID = @PurchaseID", new { p.PurchaseID }, tx);
            foreach (var oldItem in oldItems)
            {
                conn.Execute(
                    "UPDATE Products SET Stock = Stock - @Quantity WHERE ProductID = @ProductID",
                    new { oldItem.Quantity, oldItem.ProductID }, tx);
            }

            // Delete old items
            conn.Execute("DELETE FROM PurchaseItems WHERE PurchaseID = @PurchaseID", new { p.PurchaseID }, tx);

            // Update header
            conn.Execute(
                @"UPDATE Purchases 
                  SET PurchaseDate = @PurchaseDate, SupplierID = @SupplierID, SupplierName = @SupplierName,
                      TotalAmount = @TotalAmount, CreatedBy = @CreatedBy, CreatedByName = @CreatedByName
                  WHERE PurchaseID = @PurchaseID", p, tx);

            ProcessPurchaseItems(conn, tx, p.PurchaseID, p.Items);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void ProcessPurchaseItems(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int purchaseId, IEnumerable<PurchaseItem> items)
    {
        foreach (var item in items)
        {
            item.PurchaseID = purchaseId;
            item.PackageQuantity = item.PackageQuantity > 0 ? item.PackageQuantity : item.Quantity;
            item.UnitsPerPackage = Math.Max(1, item.UnitsPerPackage);
            item.Quantity = item.TotalStockUnits;
            
            // Batch Logic
            var originalProd = conn.QuerySingleOrDefault<Product>("SELECT * FROM Products WHERE ProductID = @ProductID", new { item.ProductID }, tx);
            if (originalProd != null && string.Compare(originalProd.BatchNumber ?? "", item.BatchNumber ?? "", true) != 0)
            {
                var existingBatchProd = conn.QueryFirstOrDefault<int?>(
                    "SELECT ProductID FROM Products WHERE Name = @Name AND (CompanyID = @CompanyID OR (CompanyID IS NULL AND @CompanyID IS NULL)) AND (BatchNumber = @BatchNumber OR (BatchNumber IS NULL AND @BatchNumber IS NULL))",
                    new { originalProd.Name, originalProd.CompanyID, item.BatchNumber }, tx);
                
                if (existingBatchProd.HasValue)
                {
                    item.ProductID = existingBatchProd.Value;
                }
                else
                {
                    // Create new batch variant
                    var newProdId = conn.ExecuteScalar<int>(
                        @"INSERT INTO Products 
                            (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
                          VALUES 
                            (@Name, @GenericName, @CompanyID, @CompanyName, @SupplierID, @SupplierName, @BatchNumber, @Type, @Category, @Rack, @ExpiryDate, @PurchasePrice, @SellingPrice, @TabletsPerBox, 0, @MinimumStockLevel, @IsReturnable, 1);
                          SELECT SCOPE_IDENTITY();",
                        new { 
                            originalProd.Name, originalProd.GenericName, originalProd.CompanyID, originalProd.CompanyName, 
                            originalProd.SupplierID, originalProd.SupplierName, 
                            item.BatchNumber, originalProd.Type, originalProd.Category, originalProd.Rack, item.ExpiryDate, 
                            item.PurchasePrice, originalProd.SellingPrice, originalProd.TabletsPerBox,
                            originalProd.MinimumStockLevel, originalProd.IsReturnable
                        }, tx);
                    item.ProductID = newProdId;
                }
            }

            conn.Execute(
                @"INSERT INTO PurchaseItems (PurchaseID, ProductID, BatchNumber, ExpiryDate, Quantity, BonusQuantity, PackageType, PackageQuantity, UnitsPerPackage, PurchasePrice, Discount, Tax)
                  VALUES (@PurchaseID, @ProductID, @BatchNumber, @ExpiryDate, @Quantity, @BonusQuantity, @PackageType, @PackageQuantity, @UnitsPerPackage, @PurchasePrice, @Discount, @Tax)",
                item, tx);

            // Update product stock (tied to purchases)
            conn.Execute(
                "UPDATE Products SET Stock = Stock + @Quantity WHERE ProductID = @ProductID",
                new { item.Quantity, item.ProductID }, tx);
        }
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        var items = conn.Query<PurchaseItem>("SELECT * FROM PurchaseItems WHERE PurchaseID = @id", new { id });
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var item in items)
            {
                // Decrement product stock when purchase is deleted
                conn.Execute(
                    "UPDATE Products SET Stock = Stock - @Quantity WHERE ProductID = @ProductID",
                    new { item.Quantity, item.ProductID }, tx);
            }

            conn.Execute("DELETE FROM Purchases WHERE PurchaseID = @id", new { id }, tx);
            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }
}
