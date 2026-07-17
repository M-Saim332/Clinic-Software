using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ActivityLogRepository
{
    private readonly DatabaseSession _session;

    public ActivityLogRepository(DatabaseSession session) => _session = session;

    /// <summary>Inserts an activity log entry. Silent on failure so it never breaks callers.</summary>
    public void Insert(ActivityLog log)
    {
        try
        {
            using var conn = _session.CreateConnection();
            conn.Execute(@"
                IF OBJECT_ID('ActivityLogs', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO ActivityLogs (Title, Description, Module, UserId, UserName, CreatedAt)
                    VALUES (@Title, @Description, @Module, @UserId, @UserName, @CreatedAt)
                END", log);
        }
        catch { /* silent — logging must never break business logic */ }
    }

    /// <summary>Returns the most recent activity entries (newest first).</summary>
    public IEnumerable<ActivityLog> GetLatest(int limit = 20)
    {
        try
        {
            using var conn = _session.CreateConnection();
            return conn.Query<ActivityLog>(@"
                IF OBJECT_ID('ActivityLogs', 'U') IS NOT NULL
                    SELECT TOP (@limit) * FROM ActivityLogs ORDER BY CreatedAt DESC;
                ELSE
                    SELECT TOP 0 * FROM ActivityLogs WHERE 1=0;", new { limit });
        }
        catch
        {
            return Enumerable.Empty<ActivityLog>();
        }
    }
}
