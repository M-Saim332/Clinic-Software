using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class MedicineRepository
{
    private readonly DatabaseSession _session;

    public MedicineRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Medicine> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              ORDER BY m.Name");
    }

    public Medicine? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.MedicineID = @id", new { id });
    }

    public IEnumerable<Medicine> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Name LIKE @term
                 OR m.GenericName LIKE @term
                 OR c.Name LIKE @term
              ORDER BY m.Name",
            new { term = $"%{term}%" });
    }

    public IEnumerable<Medicine> GetExpired()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.ExpiryDate IS NOT NULL AND m.ExpiryDate <= CAST(GETDATE() AS DATE)
              ORDER BY m.ExpiryDate");
    }

    public IEnumerable<Medicine> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock <= m.MinimumStockLevel
              ORDER BY m.Stock");
    }

    public IEnumerable<Medicine> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT m.*, c.Name AS CompanyName
              FROM Medicines m
              LEFT JOIN Companies c ON m.CompanyID = c.CompanyID
              WHERE m.Stock > 0
                AND (m.ExpiryDate IS NULL OR m.ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY m.Name");
    }

    public int Insert(Medicine m)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Medicines
                (Name, GenericName, CompanyID, BatchNumber, ExpiryDate, PurchasePrice, SellingPrice, Stock, MinimumStockLevel)
              VALUES
                (@Name, @GenericName, @CompanyID, @BatchNumber, @ExpiryDate, @PurchasePrice, @SellingPrice, @Stock, @MinimumStockLevel);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Medicine m)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Medicines SET
                Name = @Name, GenericName = @GenericName, CompanyID = @CompanyID,
                BatchNumber = @BatchNumber, ExpiryDate = @ExpiryDate,
                PurchasePrice = @PurchasePrice, SellingPrice = @SellingPrice,
                Stock = @Stock, MinimumStockLevel = @MinimumStockLevel
              WHERE MedicineID = @MedicineID", m);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check if there are sales referencing this medicine
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SaleItems WHERE MedicineID = @id", new { id });
        if (count > 0) return false;
        conn.Execute("DELETE FROM Medicines WHERE MedicineID = @id", new { id });
        return true;
    }

    public void DecrementStock(int medicineId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Medicines SET Stock = Stock - @quantity WHERE MedicineID = @medicineId",
            new { quantity, medicineId });
    }

    public void AddStock(int medicineId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Medicines SET Stock = Stock + @quantity WHERE MedicineID = @medicineId",
            new { quantity, medicineId });
    }
}
