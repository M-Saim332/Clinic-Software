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
        try
        {
            using var conn = _session.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Step 1: Delete SaleItems that reference Medicines belonging to this company
            conn.Execute(@"
                DELETE FROM SaleItems 
                WHERE MedicineID IN (SELECT MedicineID FROM Medicines WHERE CompanyID = @id)",
                new { id }, tx);

            // Step 2: Delete Medicines belonging to this company
            conn.Execute("DELETE FROM Medicines WHERE CompanyID = @id", new { id }, tx);

            // Step 3: Delete SaleItems referencing Products from this company (empty table but safe to run)
            conn.Execute(@"
                DELETE FROM SaleItems 
                WHERE MedicineID IN (SELECT ProductID FROM Products WHERE CompanyID = @id)",
                new { id }, tx);

            // Step 4: Delete PurchaseItems for products from this company
            conn.Execute(@"
                DELETE FROM PurchaseItems 
                WHERE ProductID IN (SELECT ProductID FROM Products WHERE CompanyID = @id)",
                new { id }, tx);

            // Step 5: Delete Products from this company
            conn.Execute("DELETE FROM Products WHERE CompanyID = @id", new { id }, tx);

            // Step 6: Delete the Company itself
            conn.Execute("DELETE FROM Companies WHERE CompanyID = @id", new { id }, tx);

            tx.Commit();
            return true;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Company Delete Failed: {ex}");
            return false;
        }
    }
}
