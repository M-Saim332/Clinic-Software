using ClinicSystem.Core.Models;
using Dapper;
using BC = BCrypt.Net.BCrypt;

namespace ClinicSystem.Data.Repositories;

/// <summary>Result of an authentication attempt — carries both the user and the reason for any failure.</summary>
public enum LoginBlockReason { None, InvalidCredentials, AccountInactive }

public record AuthResult(User? User, LoginBlockReason Reason);

public class UserRepository
{
    private readonly DatabaseSession _session;

    public UserRepository(DatabaseSession session) => _session = session;

    // ── Queries ─────────────────────────────────────────────────────────────

    public IEnumerable<User> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<User>("SELECT * FROM Users ORDER BY FullName, Username");
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

    public bool IsUsernameTaken(string username, int excludeUserId = 0)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE Username = @username AND UserID <> @excludeUserId",
            new { username, excludeUserId }) > 0;
    }

    public bool IsEmailTaken(string email, int excludeUserId = 0)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE Email = @email AND UserID <> @excludeUserId",
            new { email, excludeUserId }) > 0;
    }

    // ── Authentication ───────────────────────────────────────────────────────

    /// <summary>Authenticates credentials with status awareness. Returns detailed AuthResult.</summary>
    public AuthResult AuthenticateDetailed(string username, string password)
    {
        var user = GetByUsername(username);
        if (user == null) return new AuthResult(null, LoginBlockReason.InvalidCredentials);
        if (!BC.Verify(password, user.PasswordHash)) return new AuthResult(null, LoginBlockReason.InvalidCredentials);
        if (!user.IsActive) return new AuthResult(user, LoginBlockReason.AccountInactive);
        return new AuthResult(user, LoginBlockReason.None);
    }

    /// <summary>Legacy authenticate — returns user only on full success (active + correct password).</summary>
    public User? Authenticate(string username, string password)
    {
        var result = AuthenticateDetailed(username, password);
        return result.Reason == LoginBlockReason.None ? result.User : null;
    }

    /// <summary>Records the current UTC time as the user's last login.</summary>
    public void RecordLastLogin(int userId)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET LastLogin = GETDATE() WHERE UserID = @userId",
            new { userId });
    }

    // ── Insert ───────────────────────────────────────────────────────────────

    /// <summary>Creates a new user with all profile fields. Throws if username/email already taken.</summary>
    public int Insert(User u, string plainPassword)
    {
        if (IsUsernameTaken(u.Username))
            throw new InvalidOperationException($"Username '{u.Username}' is already taken.");
        if (!string.IsNullOrWhiteSpace(u.Email) && IsEmailTaken(u.Email))
            throw new InvalidOperationException($"Email '{u.Email}' is already registered.");

        u.PasswordHash = BC.HashPassword(plainPassword);
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO Users
                (Username, PasswordHash, Role, FullName, IsActive, Permissions,
                 Email, Phone, CNIC, Address, Gender, Qualification, Designation,
                 LicenseNumber, DateOfBirth, ProfilePicture, ForcePasswordChange)
            VALUES
                (@Username, @PasswordHash, @Role, @FullName, @IsActive, @Permissions,
                 @Email, @Phone, @CNIC, @Address, @Gender, @Qualification, @Designation,
                 @LicenseNumber, @DateOfBirth, @ProfilePicture, @ForcePasswordChange);
            SELECT SCOPE_IDENTITY();", u);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    /// <summary>Updates role, status, and permissions (admin form — no profile details).</summary>
    public void Update(User u)
    {
        var existing = GetById(u.UserID);
        if (existing?.Role == "Admin") return; // guard: cannot strip admin role

        using var conn = _session.CreateConnection();
        conn.Execute(@"
            UPDATE Users SET
                Username = @Username,
                Role = @Role,
                FullName = @FullName,
                IsActive = @IsActive,
                Permissions = @Permissions,
                UpdatedAt = GETDATE()
            WHERE UserID = @UserID", u);
    }

    /// <summary>Full admin update: all profile fields + role + status + permissions.</summary>
    public void AdminUpdateFull(User u)
    {
        if (IsUsernameTaken(u.Username, u.UserID))
            throw new InvalidOperationException($"Username '{u.Username}' is already taken.");
        if (!string.IsNullOrWhiteSpace(u.Email) && IsEmailTaken(u.Email, u.UserID))
            throw new InvalidOperationException($"Email '{u.Email}' is already registered.");

        using var conn = _session.CreateConnection();
        conn.Execute(@"
            UPDATE Users SET
                Username        = @Username,
                FullName        = @FullName,
                Role            = @Role,
                IsActive        = @IsActive,
                Permissions     = @Permissions,
                Email           = @Email,
                Phone           = @Phone,
                CNIC            = @CNIC,
                Address         = @Address,
                Gender          = @Gender,
                Qualification   = @Qualification,
                Designation     = @Designation,
                LicenseNumber   = @LicenseNumber,
                DateOfBirth     = @DateOfBirth,
                ProfilePicture  = @ProfilePicture,
                UpdatedAt       = GETDATE()
            WHERE UserID = @UserID", u);
    }

    // ── Password ─────────────────────────────────────────────────────────────

    public void ChangePassword(int userId, string newPlainPassword)
    {
        var hash = BC.HashPassword(newPlainPassword);
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET PasswordHash = @hash, ForcePasswordChange = 0, UpdatedAt = GETDATE() WHERE UserID = @userId",
            new { hash, userId });
    }

    /// <summary>Admin resets another user's password. Can optionally force change on next login.</summary>
    public void AdminResetPassword(int userId, string newPlainPassword, bool forceChangeOnNextLogin = false)
    {
        var hash = BC.HashPassword(newPlainPassword);
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET PasswordHash = @hash, ForcePasswordChange = @forceChangeOnNextLogin, UpdatedAt = GETDATE() WHERE UserID = @userId",
            new { hash, userId, forceChangeOnNextLogin });
    }

    // ── Status ───────────────────────────────────────────────────────────────

    /// <summary>Quick toggle — activate or deactivate a user account.</summary>
    public void ToggleStatus(int userId, bool isActive)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET IsActive = @isActive, UpdatedAt = GETDATE() WHERE UserID = @userId",
            new { isActive, userId });
    }

    // ── Profile (self-service) ───────────────────────────────────────────────

    public void UpdateProfile(int userId, string fullName, string username)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Users SET FullName = @fullName, Username = @username, UpdatedAt = GETDATE() WHERE UserID = @userId",
            new { fullName, username, userId });
    }

    public void UpdateFullProfile(int userId, User u)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"
            UPDATE Users SET
                FullName        = @FullName,
                Username        = @Username,
                Email           = @Email,
                Phone           = @Phone,
                CNIC            = @CNIC,
                Address         = @Address,
                Gender          = @Gender,
                Qualification   = @Qualification,
                Designation     = @Designation,
                LicenseNumber   = @LicenseNumber,
                DateOfBirth     = @DateOfBirth,
                ProfilePicture  = @ProfilePicture,
                UpdatedAt       = GETDATE()
            WHERE UserID = @userId",
            new
            {
                userId,
                u.FullName, u.Username, u.Email, u.Phone, u.CNIC, u.Address, u.Gender,
                u.Qualification, u.Designation, u.LicenseNumber, u.DateOfBirth, u.ProfilePicture
            });
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
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
