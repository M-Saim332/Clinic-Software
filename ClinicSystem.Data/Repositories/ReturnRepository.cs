using ClinicSystem.Core.Models;
using Dapper;
using System.Collections.Generic;

namespace ClinicSystem.Data.Repositories;

public class ReturnRepository
{
    private readonly DatabaseSession _session;

    public ReturnRepository(DatabaseSession session) => _session = session;

    public IEnumerable<ProductReturn> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductReturn>(
            @"SELECT r.*, p.Name AS ProductName, u.FullName AS CreatedByName,
                     pat.Name AS PatientName, sup.Name AS SupplierName
              FROM Returns r
              JOIN Products p ON r.ProductId = p.ProductId
              LEFT JOIN Users u ON r.CreatedBy = u.UserID
              LEFT JOIN Patients pat ON r.PatientId = pat.PatientID
              LEFT JOIN Suppliers sup ON r.SupplierId = sup.SupplierID
              ORDER BY r.CreatedAt DESC");
    }

    public IEnumerable<ProductReturn> GetByRange(DateTime fromInclusive, DateTime toExclusive, string? processedByName = null)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductReturn>(
            @"SELECT r.*, p.Name AS ProductName, u.FullName AS CreatedByName,
                     pat.Name AS PatientName, sup.Name AS SupplierName
              FROM Returns r
              JOIN Products p ON r.ProductId = p.ProductId
              LEFT JOIN Users u ON r.CreatedBy = u.UserID
              LEFT JOIN Patients pat ON r.PatientId = pat.PatientID
              LEFT JOIN Suppliers sup ON r.SupplierId = sup.SupplierID
              WHERE r.CreatedAt >= @fromInclusive AND r.CreatedAt < @toExclusive
                AND (@processedByName IS NULL OR u.FullName = @processedByName)
              ORDER BY r.CreatedAt DESC", new { fromInclusive, toExclusive, processedByName });
    }

    public void Insert(ProductReturn ret)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (ret.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(ret.Quantity));
            ret.StockQuantity = ret.StockQuantity > 0 ? ret.StockQuantity : ret.Quantity;
            var returnId = conn.ExecuteScalar<int>(
                @"INSERT INTO Returns (ReturnNo, ProductId, BatchNo, Quantity, UnitType, StockQuantity, ReturnType, Reason, Notes, PatientId, SupplierId, SaleId, RefundAmount, CreatedBy, CreatedAt)
                  VALUES (@ReturnNo, @ProductId, @BatchNo, @Quantity, @UnitType, @StockQuantity, @ReturnType, @Reason, @Notes, @PatientId, @SupplierId, @SaleId, @RefundAmount, @CreatedBy, @CreatedAt);
                  SELECT SCOPE_IDENTITY();", ret, tx);
            ret.ReturnId = returnId;

            // Update inventory based on return type
            if (ret.ReturnType == "Patient Return")
            {
                // Patient returned product to clinic => Stock increases
                var updated = conn.Execute("UPDATE Products SET Stock = Stock + @StockQuantity WHERE ProductId = @ProductId AND IsActive = 1", new { ret.StockQuantity, ret.ProductId }, tx);
                if (updated != 1) throw new InvalidOperationException("The selected product is unavailable.");
            }
            else if (ret.ReturnType == "Supplier Return")
            {
                // Clinic returned product to supplier => Stock decreases
                var updated = conn.Execute("UPDATE Products SET Stock = Stock - @StockQuantity WHERE ProductId = @ProductId AND IsActive = 1 AND Stock >= @StockQuantity", new { ret.StockQuantity, ret.ProductId }, tx);
                if (updated != 1) throw new InvalidOperationException("Insufficient stock for this supplier return.");
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public decimal GetTodayTotalPatientReturns()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(RefundAmount) FROM Returns 
              WHERE ReturnType = 'Patient Return' AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)") ?? 0;
    }

    public decimal GetTodayTotalSupplierReturns()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(RefundAmount) FROM Returns 
              WHERE ReturnType = 'Supplier Return' AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)") ?? 0;
    }

    public IEnumerable<dynamic> GetDailyPatientReturnsLast30Days()
    {
        using var conn = _session.CreateConnection();
        return conn.Query(
            @"SELECT CAST(CreatedAt AS DATE) AS Date, SUM(RefundAmount) AS Total
              FROM Returns
              WHERE ReturnType = 'Patient Return' AND CreatedAt >= DATEADD(day, -30, GETDATE())
              GROUP BY CAST(CreatedAt AS DATE)");
    }

    public IEnumerable<dynamic> GetDailySupplierReturnsLast30Days()
    {
        using var conn = _session.CreateConnection();
        return conn.Query(
            @"SELECT CAST(CreatedAt AS DATE) AS Date, SUM(RefundAmount) AS Total
              FROM Returns
              WHERE ReturnType = 'Supplier Return' AND CreatedAt >= DATEADD(day, -30, GETDATE())
              GROUP BY CAST(CreatedAt AS DATE)");
    }
}
