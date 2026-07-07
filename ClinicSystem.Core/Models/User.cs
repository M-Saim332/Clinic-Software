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
    public DateTime? CreatedAt { get; set; }

    public UserRole UserRole =>
        Enum.TryParse<UserRole>(Role, out var r) ? r : UserRole.Receptionist;

    public bool IsDoctor => UserRole == UserRole.Doctor || UserRole == UserRole.Admin;
    public bool IsAdmin => UserRole == UserRole.Admin;
    public string DisplayName => Username;
}
