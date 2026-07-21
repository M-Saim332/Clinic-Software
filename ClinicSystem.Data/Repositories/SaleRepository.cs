using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class SaleRepository
{
    private readonly DatabaseSession _session;

    public SaleRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Sale> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>(
            @"SELECT s.*, COALESCE(p.Name, s.PatientName) AS PatientName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              ORDER BY s.SaleDate DESC");
    }

    public int GetCountForDate(DateTime date)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Sales WHERE CAST(SaleDate AS DATE) = @date",
            new { date });
    }

    public Sale? GetByIdWithItems(int id)
    {
        using var conn = _session.CreateConnection();
        var sale = conn.QuerySingleOrDefault<Sale>(
            @"SELECT s.*, p.Name AS PatientName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              WHERE s.SaleID = @id", new { id });

        if (sale == null) return null;

        sale.Items = conn.Query<SaleItem>(
            @"SELECT si.*, m.Name AS ProductName, m.SellingPrice AS ProductPrice
              FROM SaleItems si
              JOIN Products m ON si.ProductID = m.ProductID
              WHERE si.SaleID = @id", new { id }).ToList();

        return sale;
    }

    public int Insert(Sale s)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var saleId = conn.ExecuteScalar<int>(
                @"INSERT INTO Sales (InvoiceNumber, SaleDate, PatientID, PatientName, ConsultationFee, GrandTotal, PaymentMethod, IsPosted)
                  VALUES (@InvoiceNumber, @SaleDate, @PatientID, @PatientName, @ConsultationFee, @GrandTotal, @PaymentMethod, @IsPosted);
                  SELECT SCOPE_IDENTITY();", s, tx);

            foreach (var item in s.Items)
            {
                item.SaleID = saleId;
                conn.Execute(
                    @"INSERT INTO SaleItems (SaleID, ProductID, Quantity, Discount, Tax, LineTotal)
                      VALUES (@SaleID, @ProductID, @Quantity, @Discount, @Tax, @LineTotal)",
                    item, tx);

                // If posted on insert, decrement product stock
                if (s.IsPosted)
                {
                    conn.Execute(
                        "UPDATE Products SET Stock = Stock - @Quantity WHERE ProductID = @ProductID",
                        new { item.Quantity, item.ProductID }, tx);
                }
            }

            tx.Commit();
            return saleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Sale s)
    {
        using var conn = _session.CreateConnection();
        // Check if already posted in DB
        var current = conn.QuerySingleOrDefault<Sale>("SELECT * FROM Sales WHERE SaleID = @SaleID", new { s.SaleID });
        if (current == null) return;
        if (current.IsPosted)
        {
            throw new InvalidOperationException("This sale has already been posted and cannot be edited.");
        }

        using var tx = conn.BeginTransaction();
        try
        {
            conn.Execute(
                @"UPDATE Sales SET
                    InvoiceNumber = @InvoiceNumber, SaleDate = @SaleDate, PatientID = @PatientID,
                    ConsultationFee = @ConsultationFee, GrandTotal = @GrandTotal, PaymentMethod = @PaymentMethod, IsPosted = @IsPosted
                  WHERE SaleID = @SaleID", s, tx);

            // Delete old items
            conn.Execute("DELETE FROM SaleItems WHERE SaleID = @SaleID", new { s.SaleID }, tx);

            // Insert new items
            foreach (var item in s.Items)
            {
                item.SaleID = s.SaleID;
                conn.Execute(
                    @"INSERT INTO SaleItems (SaleID, ProductID, Quantity, Discount, Tax, LineTotal)
                      VALUES (@SaleID, @ProductID, @Quantity, @Discount, @Tax, @LineTotal)",
                    item, tx);

                // If now posting, decrement product stock
                if (s.IsPosted)
                {
                    conn.Execute(
                        "UPDATE Products SET Stock = Stock - @Quantity WHERE ProductID = @ProductID",
                        new { item.Quantity, item.ProductID }, tx);
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            var current = conn.QuerySingleOrDefault<Sale>("SELECT * FROM Sales WHERE SaleID = @id", new { id });
            if (current == null) return false;
            if (current.IsPosted) return false;

            conn.Execute("DELETE FROM Sales WHERE SaleID = @id", new { id });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
