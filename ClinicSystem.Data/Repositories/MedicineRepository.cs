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
        return conn.Query<Medicine>("SELECT * FROM Medicines ORDER BY Name");
    }

    public Medicine? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Medicine>(
            "SELECT * FROM Medicines WHERE MedicineID = @id", new { id });
    }

    public IEnumerable<Medicine> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            "SELECT * FROM Medicines WHERE Name LIKE @term OR Manufacturer LIKE @term ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public IEnumerable<Medicine> GetExpired()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            "SELECT * FROM Medicines WHERE ExpiryDate IS NOT NULL AND ExpiryDate <= CAST(GETDATE() AS DATE) ORDER BY ExpiryDate");
    }

    public IEnumerable<Medicine> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            "SELECT * FROM Medicines WHERE Stock <= MinStock ORDER BY Stock");
    }

    /// <summary>Returns medicines valid for prescribing (not expired, in stock).</summary>
    public IEnumerable<Medicine> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT * FROM Medicines
              WHERE Stock > 0
                AND (ExpiryDate IS NULL OR ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY Name");
    }

    public int Insert(Medicine m)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Medicines (Name, Stock, MinStock, ExpiryDate, Price, Manufacturer, Category)
              VALUES (@Name, @Stock, @MinStock, @ExpiryDate, @Price, @Manufacturer, @Category);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Medicine m)
    {
        m.UpdatedAt = DateTime.Now;
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Medicines SET
                Name = @Name, Stock = @Stock, MinStock = @MinStock,
                ExpiryDate = @ExpiryDate, Price = @Price,
                Manufacturer = @Manufacturer, Category = @Category,
                UpdatedAt = @UpdatedAt
              WHERE MedicineID = @MedicineID", m);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM PrescriptionItems WHERE MedicineID = @id", new { id });
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
}
