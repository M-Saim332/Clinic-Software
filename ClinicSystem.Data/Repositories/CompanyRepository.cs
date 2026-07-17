using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class CompanyRepository
{
    private readonly DatabaseSession _session;

    public CompanyRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Company> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Company>("SELECT * FROM Companies ORDER BY Name");
    }

    public Company? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Company>(
            "SELECT * FROM Companies WHERE CompanyID = @id", new { id });
    }

    public IEnumerable<Company> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Company>(
            @"SELECT * FROM Companies
              WHERE Name LIKE @term OR Phone LIKE @term OR Email LIKE @term
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Company c)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Companies (Name, Address, Phone, Email)
              VALUES (@Name, @Address, @Phone, @Email);
              SELECT SCOPE_IDENTITY();", c);
    }

    public void Update(Company c)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Companies SET
                Name = @Name, Address = @Address, Phone = @Phone, Email = @Email
              WHERE CompanyID = @CompanyID", c);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check if referenced by products
        var productCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Products WHERE CompanyID = @id", new { id });
        if (productCount > 0) return false;

        conn.Execute("DELETE FROM Companies WHERE CompanyID = @id", new { id });
        return true;
    }
}
