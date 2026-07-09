using ClinicSystem.Core.Models;
using Dapper;
using BC = BCrypt.Net.BCrypt;

namespace ClinicSystem.Data.Repositories;

public class UserRepository
{
    private readonly DatabaseSession _session;

    public UserRepository(DatabaseSession session) => _session = session;

    public IEnumerable<User> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<User>("SELECT * FROM Users ORDER BY Username");
    }

    public User? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<User>(
            "SELECT * FROM Users WHERE UserID = @id", new { id });
    }

    public User? GetByUsername(string username)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<User>(
            "SELECT * FROM Users WHERE Username = @username", new { username });
    }

    /// <summary>Authenticates credentials. Returns the User if valid, null otherwise.</summary>
    public User? Authenticate(string username, string password)
    {
        var user = GetByUsername(username);
        if (user == null) return null;
        return BC.Verify(password, user.PasswordHash) ? user : null;
    }

    public int Insert(User u, string plainPassword)
    {
        u.PasswordHash = BC.HashPassword(plainPassword);
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive, Permissions)
              VALUES (@Username, @PasswordHash, @Role, @FullName, @IsActive, @Permissions);
              SELECT SCOPE_IDENTITY();", u);
    }

    public void Update(User u)
    {
        // Backend guard: Admin account must never be modified via this path
        var existing = GetById(u.UserID);
        if (existing?.Role == "Admin") return;

        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Users SET
                Username = @Username, Role = @Role, FullName = @FullName, IsActive = @IsActive, Permissions = @Permissions
              WHERE UserID = @UserID", u);
    }

    public void ChangePassword(int userId, string newPlainPassword)
    {
        var hash = BC.HashPassword(newPlainPassword);
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET PasswordHash = @hash WHERE UserID = @userId",
            new { hash, userId });
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check all foreign-key references before attempting delete
        var apptCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Appointments WHERE DoctorID = @id", new { id });
        if (apptCount > 0) return false;

        var rxCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Prescriptions WHERE DoctorID = @id", new { id });
        if (rxCount > 0) return false;

        conn.Execute("DELETE FROM Users WHERE UserID = @id", new { id });
        return true;
    }
}
