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
