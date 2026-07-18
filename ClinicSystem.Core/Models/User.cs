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

    public UserRole UserRole =>
        Enum.TryParse<UserRole>(Role, out var r) ? r : UserRole.Receptionist;

    public bool IsDoctor => UserRole == UserRole.Doctor || UserRole == UserRole.Admin;
    public bool IsAdmin => UserRole == UserRole.Admin;
    public bool IsPharmacist => UserRole == UserRole.Pharmacist;
    public bool IsDeletable => Role != "Admin";
    public bool IsEditable => Role != "Admin";
    public string DisplayName => Username;

    public bool HasAccess(string module)
    {
        if (IsAdmin || UserRole == UserRole.Doctor) return true;
        return Permissions?.Contains(module) ?? false;
    }
}
