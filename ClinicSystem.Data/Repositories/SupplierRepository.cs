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
        return conn.Query<Supplier>("SELECT * FROM Suppliers ORDER BY SCode, Name");
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
                 OR SCode = TRY_CONVERT(INT, @rawTerm)
              ORDER BY SCode, Name",
            new { term = $"%{term}%", rawTerm = term.Trim() });
    }

    public int GetNextSCode()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT ISNULL(MAX(SCode), 0) + 1 FROM Suppliers");
    }

    public int Insert(Supplier s)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        s.SCode = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(SCode), 0) + 1 FROM Suppliers WITH (UPDLOCK, HOLDLOCK)", transaction: tx);
        var id = conn.ExecuteScalar<int>(
            @"INSERT INTO Suppliers (SCode, Name, Address, Phone, Email, CNIC)
              VALUES (@SCode, @Name, @Address, @Phone, @Email, @CNIC);
              SELECT CONVERT(INT, SCOPE_IDENTITY());", s, tx);
        tx.Commit();
        return id;
    }

    public void Update(Supplier s)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Suppliers SET
                Name = @Name, Address = @Address, Phone = @Phone, Email = @Email, CNIC = @CNIC
              WHERE SupplierID = @SupplierID", s);
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            var referenced = conn.ExecuteScalar<int>(@"SELECT
                (SELECT COUNT(*) FROM Purchases WHERE SupplierID=@id) +
                (SELECT COUNT(*) FROM Products WHERE SupplierID=@id) +
                (SELECT COUNT(*) FROM Returns WHERE SupplierId=@id)", new { id });
            if (referenced > 0) return false;
            return conn.Execute("DELETE FROM Suppliers WHERE SupplierID=@id", new { id }) == 1;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Supplier Delete Failed: {ex}");
            return false;
        }
    }
}
