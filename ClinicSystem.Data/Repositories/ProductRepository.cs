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
            @"SELECT p.*, c.Name AS CompanyName
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              ORDER BY p.Name");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Product>(
            @"SELECT p.*, c.Name AS CompanyName
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              WHERE p.ProductID = @id", new { id });
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT p.*, c.Name AS CompanyName
              FROM Products p
              LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
              WHERE p.Name LIKE @term OR c.Name LIKE @term
              ORDER BY p.Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Product p)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Products (CompanyID, Name, PurchaseRate, SellingPrice, Tax, StockQuantity)
              VALUES (@CompanyID, @Name, @PurchaseRate, @SellingPrice, @Tax, @StockQuantity);
              SELECT SCOPE_IDENTITY();", p);
    }

    public void Update(Product p)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Products SET
                CompanyID = @CompanyID, Name = @Name, PurchaseRate = @PurchaseRate,
                SellingPrice = @SellingPrice, Tax = @Tax, StockQuantity = @StockQuantity
              WHERE ProductID = @ProductID", p);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check if referenced by purchase items
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM PurchaseItems WHERE ProductID = @id", new { id });
        if (count > 0) return false;

        conn.Execute("DELETE FROM Products WHERE ProductID = @id", new { id });
        return true;
    }

    public void AddStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Products SET StockQuantity = StockQuantity + @quantity WHERE ProductID = @productId",
            new { quantity, productId });
    }
}
