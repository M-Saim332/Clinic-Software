using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ReturnRepository
{
    private readonly DatabaseSession _session;

    public ReturnRepository(DatabaseSession session) => _session = session;

    public IEnumerable<MedicineReturn> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<MedicineReturn>(
            @"SELECT r.*, m.Name AS MedicineName, p.Name AS PatientName, s.InvoiceNumber, u.FullName AS ProcessedByName
              FROM MedicineReturns r
              JOIN Medicines m ON r.MedicineId = m.MedicineId
              JOIN Patients p ON r.PatientId = p.PatientId
              JOIN Sales s ON r.SaleId = s.SaleId
              LEFT JOIN Users u ON r.ProcessedBy = u.UserID
              ORDER BY r.ReturnDate DESC");
    }

    public IEnumerable<MedicineReturn> GetBySaleId(int saleId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<MedicineReturn>(
            "SELECT * FROM MedicineReturns WHERE SaleId = @saleId", new { saleId });
    }

    public void Insert(MedicineReturn ret)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Verify item is returnable
            var med = conn.QuerySingleOrDefault<Medicine>("SELECT * FROM Medicines WHERE MedicineId = @MedicineId", new { ret.MedicineId }, tx);
            if (med != null && !med.IsReturnable)
            {
                throw new InvalidOperationException($"Medicine '{med.Name}' is not returnable.");
            }

            // Verify quantity against sale
            var saleItem = conn.QuerySingleOrDefault<SaleItem>(
                "SELECT * FROM SaleItems WHERE SaleID = @SaleId AND MedicineID = @MedicineId", 
                new { ret.SaleId, ret.MedicineId }, tx);
                
            if (saleItem == null)
            {
                throw new InvalidOperationException("This medicine was not found on the specified sale.");
            }

            // Check already returned quantity
            var alreadyReturned = conn.ExecuteScalar<int?>(
                "SELECT SUM(QuantityReturned) FROM MedicineReturns WHERE SaleId = @SaleId AND MedicineId = @MedicineId", 
                new { ret.SaleId, ret.MedicineId }, tx) ?? 0;

            if (ret.QuantityReturned > (saleItem.Quantity - alreadyReturned))
            {
                throw new InvalidOperationException("Cannot return more than the remaining quantity from the sale.");
            }

            // Insert return record
            var returnId = conn.ExecuteScalar<int>(
                @"INSERT INTO MedicineReturns (SaleId, MedicineId, PatientId, QuantityReturned, UnitPriceAtSale, RefundAmount, Reason, ReturnDate, ProcessedBy, Status)
                  VALUES (@SaleId, @MedicineId, @PatientId, @QuantityReturned, @UnitPriceAtSale, @RefundAmount, @Reason, @ReturnDate, @ProcessedBy, @Status);
                  SELECT SCOPE_IDENTITY();", ret, tx);
            ret.ReturnId = returnId;

            // Restore stock
            conn.Execute(
                "UPDATE Medicines SET Stock = Stock + @QuantityReturned WHERE MedicineId = @MedicineId", 
                new { ret.QuantityReturned, ret.MedicineId }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
