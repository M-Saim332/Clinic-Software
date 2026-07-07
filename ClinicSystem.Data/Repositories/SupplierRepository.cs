using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class SupplierRepository
{
    private readonly DatabaseSession _session;

    public SupplierRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Supplier> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Supplier>("SELECT * FROM Suppliers ORDER BY Name");
    }

    public Supplier? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Supplier>(
            "SELECT * FROM Suppliers WHERE SupplierID = @id", new { id });
    }

    public IEnumerable<Supplier> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Supplier>(
            @"SELECT * FROM Suppliers
              WHERE Name LIKE @term OR Phone LIKE @term OR Email LIKE @term
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Supplier s)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Suppliers (Name, Address, Phone, Email)
              VALUES (@Name, @Address, @Phone, @Email);
              SELECT SCOPE_IDENTITY();", s);
    }

    public void Update(Supplier s)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Suppliers SET
                Name = @Name, Address = @Address, Phone = @Phone, Email = @Email
              WHERE SupplierID = @SupplierID", s);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check if referenced by purchases
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Purchases WHERE SupplierID = @id", new { id });
        if (count > 0) return false;

        conn.Execute("DELETE FROM Suppliers WHERE SupplierID = @id", new { id });
        return true;
    }
}
