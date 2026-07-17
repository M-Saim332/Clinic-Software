using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ProductRepository
{
    private readonly DatabaseSession _session;

    public ProductRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Product> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              ORDER BY m.Name");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.ProductID = @id", new { id });
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Name LIKE @term
                 OR m.GenericName LIKE @term
                 OR c.Name LIKE @term
              ORDER BY m.Name",
            new { term = $"%{term}%" });
    }

    public IEnumerable<Product> GetExpired()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.ExpiryDate IS NOT NULL AND m.ExpiryDate <= CAST(GETDATE() AS DATE)
              ORDER BY m.ExpiryDate");
    }

    public IEnumerable<Product> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock <= m.MinimumStockLevel
              ORDER BY m.Stock");
    }

    public IEnumerable<Product> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Products m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock > 0
                AND (m.ExpiryDate IS NULL OR m.ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY m.Name");
    }

    public int Insert(Product m)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Products
                (Name, GenericName, CompanyID, CompanyName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, Stock, MinimumStockLevel)
              VALUES
                (@Name, @GenericName, @CompanyID, @CompanyName, @BatchNumber, @Type, @Category, @Rack, @ExpiryDate, @PurchasePrice, @SellingPrice, @Stock, @MinimumStockLevel);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Product m)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Products SET
                Name = @Name, GenericName = @GenericName, CompanyID = @CompanyID, CompanyName = @CompanyName,
                BatchNumber = @BatchNumber, Type = @Type, Category = @Category, Rack = @Rack,
                ExpiryDate = @ExpiryDate, PurchasePrice = @PurchasePrice, SellingPrice = @SellingPrice,
                Stock = @Stock, MinimumStockLevel = @MinimumStockLevel
              WHERE ProductID = @ProductID", m);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check if there are sales referencing this product
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SaleItems WHERE ProductID = @id", new { id });
        if (count > 0) return false;
        conn.Execute("DELETE FROM Products WHERE ProductID = @id", new { id });
        return true;
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
