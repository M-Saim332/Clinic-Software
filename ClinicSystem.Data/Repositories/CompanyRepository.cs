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
        return conn.Query<Company>("SELECT * FROM Companies ORDER BY CCode, Name");
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
                 OR CCode = TRY_CONVERT(INT, @rawTerm)
              ORDER BY CCode, Name",
            new { term = $"%{term}%", rawTerm = term.Trim() });
    }

    public int GetNextCCode()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT ISNULL(MAX(CCode), 0) + 1 FROM Companies");
    }

    public int Insert(Company c)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        c.CCode = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(CCode), 0) + 1 FROM Companies WITH (UPDLOCK, HOLDLOCK)", transaction: tx);
        var id = conn.ExecuteScalar<int>(
            @"INSERT INTO Companies (CCode, Name, Address, Phone, Email)
              VALUES (@CCode, @Name, @Address, @Phone, @Email);
              SELECT CONVERT(INT, SCOPE_IDENTITY());", c, tx);
        tx.Commit();
        return id;
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
        try
        {
            using var conn = _session.CreateConnection();
            if (conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE CompanyID=@id", new { id }) > 0) return false;
            return conn.Execute("DELETE FROM Companies WHERE CompanyID=@id", new { id }) == 1;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Company Delete Failed: {ex}");
            return false;
        }
    }
}
