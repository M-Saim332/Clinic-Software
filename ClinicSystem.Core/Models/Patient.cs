namespace ClinicSystem.Core.Models;

public class Patient
{
    public int PatientID { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Contact { get; set; }
    public string? Address { get; set; }
    public string? MedicalHistory { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Derived display string for lists/overlays.</summary>
    public string DisplayText => $"{Name} — {Contact ?? "No contact"} — Age: {Age?.ToString() ?? "N/A"}";
}
