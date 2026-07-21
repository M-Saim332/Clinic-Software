using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class DiscountRefundRepository
{
    private readonly DatabaseSession _session;

    public DiscountRefundRepository(DatabaseSession session) => _session = session;

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>All pending (not yet paid back) refunds, newest first.</summary>
    public IEnumerable<DiscountRefund> GetAllPending()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<DiscountRefund>(
            @"SELECT * FROM DiscountRefunds
              WHERE IsCompleted = 0
              ORDER BY ApprovedAt DESC");
    }

    /// <summary>Completed refunds for history/audit log, newest first.</summary>
    public IEnumerable<DiscountRefund> GetAllHistory(int maxRows = 200)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<DiscountRefund>(
            @"SELECT TOP (@maxRows) * FROM DiscountRefunds
              WHERE IsCompleted = 1
              ORDER BY CompletedAt DESC",
            new { maxRows });
    }

    /// <summary>All refunds (pending + history) for full audit log.</summary>
    public IEnumerable<DiscountRefund> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<DiscountRefund>(
            "SELECT * FROM DiscountRefunds ORDER BY ApprovedAt DESC");
    }

    public decimal GetTodayTotalCompleted()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(RefundAmount), 0) FROM DiscountRefunds WHERE IsCompleted = 1 AND CAST(CompletedAt AS DATE) = CAST(GETDATE() AS DATE)");
    }

    public IEnumerable<(DateTime Date, decimal Total)> GetDailyTotalsLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(
            @"SELECT CAST(CompletedAt AS DATE) AS RefundDay,
                     ISNULL(SUM(RefundAmount), 0) AS Total
              FROM DiscountRefunds
              WHERE IsCompleted = 1 AND CompletedAt >= DATEADD(day, -29, CAST(GETDATE() AS DATE))
              GROUP BY CAST(CompletedAt AS DATE)
              ORDER BY RefundDay");
        return rows.Select(r => ((DateTime)r.RefundDay, (decimal)r.Total)).ToList();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Doctor inserts a new discount refund notification.</summary>
    public int Insert(DiscountRefund r)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO DiscountRefunds
                (PatientName, TokenNumber, OriginalFee, DiscountedFee, Notes,
                 ApprovedByUserID, ApprovedByName, ApprovedAt, IsCompleted)
              VALUES
                (@PatientName, @TokenNumber, @OriginalFee, @DiscountedFee, @Notes,
                 @ApprovedByUserID, @ApprovedByName, @ApprovedAt, 0);
              SELECT SCOPE_IDENTITY();",
            r);
    }

    /// <summary>Receptionist marks the refund as paid back.</summary>
    public void MarkCompleted(int refundId, int completedByUserId, string completedByName)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE DiscountRefunds SET
                IsCompleted       = 1,
                CompletedByUserID = @completedByUserId,
                CompletedByName   = @completedByName,
                CompletedAt       = GETDATE()
              WHERE RefundID = @refundId",
            new { refundId, completedByUserId, completedByName });
    }

    /// <summary>Count of pending refunds — used for badge/indicator.</summary>
    public int GetPendingCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM DiscountRefunds WHERE IsCompleted = 0");
    }
}
