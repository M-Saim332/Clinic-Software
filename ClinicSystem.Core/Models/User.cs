using ClinicSystem.Core.Enums;

namespace ClinicSystem.Core.Models;

public class User
{
    public int UserID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Receptionist";
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Permissions { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool ForcePasswordChange { get; set; } = false;

    // Profile Fields
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CNIC { get; set; }
    public string? Address { get; set; }
    public string? Gender { get; set; }
    public string? Qualification { get; set; }
    public string? Designation { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public byte[]? ProfilePicture { get; set; }

    // Computed
    public UserRole UserRole =>
        Enum.TryParse<UserRole>(Role, true, out var r) ? r : UserRole.Receptionist;

    public bool IsDoctor    => UserRole == UserRole.Doctor || UserRole == UserRole.Admin;
    public bool IsAdmin     => UserRole == UserRole.Admin || string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase);
    public bool IsPharmacist => UserRole == UserRole.Pharmacist;
    public bool IsDeletable => Role != "Admin" && !string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase);
    public bool IsEditable  => Role != "Admin" && !string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => FullName.Length > 0 ? FullName : Username;

    /// <summary>Status label shown in UI (Active / Inactive).</summary>
    public string StatusLabel => IsActive ? "Active" : "Inactive";

    /// <summary>Formatted last-login display text.</summary>
    public string LastLoginDisplay => LastLogin.HasValue
        ? LastLogin.Value.ToString("dd MMM yyyy, hh:mm tt")
        : "Never";

    public bool HasAccess(string module)
    {
        if (IsAdmin || UserRole == UserRole.Doctor) return true;
        return Permissions?.Contains(module) ?? false;
    }
}
