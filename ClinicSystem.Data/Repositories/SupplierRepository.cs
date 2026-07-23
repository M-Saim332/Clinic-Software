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
            @"INSERT INTO Suppliers (Name, Address, Phone, Email, CNIC)
              VALUES (@Name, @Address, @Phone, @Email, @CNIC);
              SELECT SCOPE_IDENTITY();", s);
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
            using var tx = conn.BeginTransaction();
            
            // Cascade delete PurchaseItems for purchases from this supplier
            conn.Execute(@"
                DELETE FROM PurchaseItems 
                WHERE PurchaseID IN (SELECT PurchaseID FROM Purchases WHERE SupplierID = @id)", 
                new { id }, tx);
                
            // Cascade delete Purchases
            conn.Execute("DELETE FROM Purchases WHERE SupplierID = @id", new { id }, tx);
            
            // Cascade delete SaleItems for products from this supplier
            conn.Execute(@"
                DELETE FROM SaleItems 
                WHERE ProductID IN (SELECT ProductID FROM Products WHERE SupplierID = @id)", 
                new { id }, tx);
                
            // Cascade delete PurchaseItems for products from this supplier (if not already covered)
            conn.Execute(@"
                DELETE FROM PurchaseItems 
                WHERE ProductID IN (SELECT ProductID FROM Products WHERE SupplierID = @id)", 
                new { id }, tx);

            // Cascade delete Products
            conn.Execute("DELETE FROM Products WHERE SupplierID = @id", new { id }, tx);
            
            // Delete Supplier
            conn.Execute("DELETE FROM Suppliers WHERE SupplierID = @id", new { id }, tx);
            
            tx.Commit();
            return true;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Supplier Delete Failed: {ex}");
            return false;
        }
    }
}
