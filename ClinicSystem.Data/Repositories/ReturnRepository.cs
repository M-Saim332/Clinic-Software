using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ReturnRepository
{
    private readonly DatabaseSession _session;

    public ReturnRepository(DatabaseSession session) => _session = session;

    public IEnumerable<ProductReturn> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductReturn>(
            @"SELECT r.*, m.Name AS ProductName, p.Name AS PatientName, s.InvoiceNumber, u.FullName AS ProcessedByName
              FROM ProductReturns r
              JOIN Products m ON r.ProductId = m.ProductId
              JOIN Patients p ON r.PatientId = p.PatientId
              JOIN Sales s ON r.SaleId = s.SaleId
              LEFT JOIN Users u ON r.ProcessedBy = u.UserID
              ORDER BY r.ReturnDate DESC");
    }

    public IEnumerable<ProductReturn> GetBySaleId(int saleId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductReturn>(
            "SELECT * FROM ProductReturns WHERE SaleId = @saleId", new { saleId });
    }

    public void Insert(ProductReturn ret)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Verify item is returnable
            var med = conn.QuerySingleOrDefault<Product>("SELECT * FROM Products WHERE ProductId = @ProductId", new { ret.ProductId }, tx);
            if (med != null && !med.IsReturnable)
            {
                throw new InvalidOperationException($"Product '{med.Name}' is not returnable.");
            }

            // Verify quantity against sale
            var saleItem = conn.QuerySingleOrDefault<SaleItem>(
                "SELECT * FROM SaleItems WHERE SaleID = @SaleId AND ProductID = @ProductId", 
                new { ret.SaleId, ret.ProductId }, tx);
                
            if (saleItem == null)
            {
                throw new InvalidOperationException("This product was not found on the specified sale.");
            }

            // Check already returned quantity
            var alreadyReturned = conn.ExecuteScalar<int?>(
                "SELECT SUM(QuantityReturned) FROM ProductReturns WHERE SaleId = @SaleId AND ProductId = @ProductId", 
                new { ret.SaleId, ret.ProductId }, tx) ?? 0;

            if (ret.QuantityReturned > (saleItem.Quantity - alreadyReturned))
            {
                throw new InvalidOperationException("Cannot return more than the remaining quantity from the sale.");
            }

            // Insert return record
            var returnId = conn.ExecuteScalar<int>(
                @"INSERT INTO ProductReturns (SaleId, ProductId, PatientId, QuantityReturned, UnitPriceAtSale, RefundAmount, Reason, ReturnDate, ProcessedBy, Status)
                  VALUES (@SaleId, @ProductId, @PatientId, @QuantityReturned, @UnitPriceAtSale, @RefundAmount, @Reason, @ReturnDate, @ProcessedBy, @Status);
                  SELECT SCOPE_IDENTITY();", ret, tx);
            ret.ReturnId = returnId;

            // Restore stock
            conn.Execute(
                "UPDATE Products SET Stock = Stock + @QuantityReturned WHERE ProductId = @ProductId", 
                new { ret.QuantityReturned, ret.ProductId }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
