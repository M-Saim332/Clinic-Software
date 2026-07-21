using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

/// <summary>
/// ProductRepository targets the 'Medicines' table in the live database.
/// MedicineID is aliased as ProductID so the Product model maps correctly.
/// </summary>
public class ProductRepository
{
    private readonly DatabaseSession _session;

    public ProductRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Product> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              ORDER BY m.Name");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Product>(
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.MedicineID = @id", new { id });
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
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
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.ExpiryDate IS NOT NULL AND m.ExpiryDate <= CAST(GETDATE() AS DATE)
              ORDER BY m.ExpiryDate");
    }

    public IEnumerable<Product> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock <= m.MinimumStockLevel
              ORDER BY m.Stock");
    }

    public IEnumerable<Product> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>(
            @"SELECT m.MedicineID AS ProductID, m.Name, m.GenericName, m.CompanyID,
                     c.Name AS CompanyName, m.BatchNumber, m.ExpiryDate,
                     m.PurchasePrice, m.SellingPrice, m.Stock, m.MinimumStockLevel
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock > 0
                AND (m.ExpiryDate IS NULL OR m.ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY m.Name");
    }

    public int Insert(Product m)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Medicines
                (Name, GenericName, CompanyID, BatchNumber, ExpiryDate, PurchasePrice, SellingPrice, Stock, MinimumStockLevel)
              VALUES
                (@Name, @GenericName, @CompanyID, @BatchNumber, @ExpiryDate, @PurchasePrice, @SellingPrice, @Stock, @MinimumStockLevel);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Product m)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Medicines SET
                Name = @Name, GenericName = @GenericName, CompanyID = @CompanyID,
                BatchNumber = @BatchNumber, ExpiryDate = @ExpiryDate,
                PurchasePrice = @PurchasePrice, SellingPrice = @SellingPrice,
                Stock = @Stock, MinimumStockLevel = @MinimumStockLevel
              WHERE MedicineID = @ProductID", m);
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Cascade delete SaleItems (SaleItems.MedicineID references Medicines.MedicineID)
            conn.Execute("DELETE FROM SaleItems WHERE MedicineID = @id", new { id }, tx);

            // Cascade delete PurchaseItems (if any reference this medicine)
            conn.Execute("DELETE FROM PurchaseItems WHERE ProductID = @id", new { id }, tx);

            // Delete the Medicine
            conn.Execute("DELETE FROM Medicines WHERE MedicineID = @id", new { id }, tx);

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
            "UPDATE Medicines SET Stock = Stock - @quantity WHERE MedicineID = @productId",
            new { quantity, productId });
    }

    public void AddStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Medicines SET Stock = Stock + @quantity WHERE MedicineID = @productId",
            new { quantity, productId });
    }
}
