using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

/// <summary>
/// ProductRepository targets the 'Products' table in the live database.
/// </summary>
public class ProductRepository
{
    private readonly DatabaseSession _session;

    public ProductRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Product> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              ORDER BY p.Name");
    }

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
    }

    public decimal GetTotalStockValue()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(CAST(PurchasePrice AS DECIMAL(18,2)) * CAST(Stock AS DECIMAL(18,2))), 0) FROM Products");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.ProductID = @id", new { id });
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.Name LIKE @term
                 OR p.GenericName LIKE @term
                 OR c.Name LIKE @term
              ORDER BY p.Name",
            new { term = $"%{term}%" });
    }

    public IEnumerable<Product> GetExpired()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.ExpiryDate IS NOT NULL AND p.ExpiryDate <= CAST(GETDATE() AS DATE)
              ORDER BY p.ExpiryDate");
    }

    public IEnumerable<Product> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.Stock <= p.MinimumStockLevel
              ORDER BY p.Stock");
    }

    public IEnumerable<Product> GetExpiringSoon(int days)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.ExpiryDate IS NOT NULL 
                AND p.ExpiryDate > CAST(GETDATE() AS DATE)
                AND p.ExpiryDate <= DATEADD(day, @days, CAST(GETDATE() AS DATE))
              ORDER BY p.ExpiryDate", new { days });
    }

    public IEnumerable<Product> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.ProductID, p.Name, p.GenericName, p.CompanyID,
                     c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName, 
                     p.BatchNumber, p.Type, p.Category, p.Rack, p.ExpiryDate,
                     p.PurchasePrice, p.SellingPrice, p.Stock, p.MinimumStockLevel
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
              WHERE p.Stock > 0
                AND (p.ExpiryDate IS NULL OR p.ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY p.Name");
    }

    public int Insert(Product m)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Products
                (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, Stock, MinimumStockLevel)
              VALUES
                (@Name, @GenericName, @CompanyID, @CompanyName, @SupplierID, @SupplierName, @BatchNumber, @Type, @Category, @Rack, @ExpiryDate, @PurchasePrice, @SellingPrice, @Stock, @MinimumStockLevel);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Product m)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Products SET
                Name = @Name, GenericName = @GenericName, CompanyID = @CompanyID, CompanyName = @CompanyName,
                SupplierID = @SupplierID, SupplierName = @SupplierName, BatchNumber = @BatchNumber,
                Type = @Type, Category = @Category, Rack = @Rack, ExpiryDate = @ExpiryDate,
                PurchasePrice = @PurchasePrice, SellingPrice = @SellingPrice,
                Stock = @Stock, MinimumStockLevel = @MinimumStockLevel
              WHERE ProductID = @ProductID", m);
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Cascade delete SaleItems
            conn.Execute("DELETE FROM SaleItems WHERE ProductID = @id", new { id }, tx);

            // Cascade delete PurchaseItems
            conn.Execute("DELETE FROM PurchaseItems WHERE ProductID = @id", new { id }, tx);

            // Cascade delete Returns
            conn.Execute("DELETE FROM Returns WHERE ProductId = @id", new { id }, tx);

            // Delete the Product
            conn.Execute("DELETE FROM Products WHERE ProductID = @id", new { id }, tx);

            tx.Commit();
            return true;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Product Delete Failed: {ex}");
            return false;
        }
    }

    public void DecrementStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Products SET Stock = Stock - @quantity WHERE ProductID = @productId",
            new { quantity, productId });
    }

    public void AddStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Products SET Stock = Stock + @quantity WHERE ProductID = @productId",
            new { quantity, productId });
    }
}
