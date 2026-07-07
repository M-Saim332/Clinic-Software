namespace ClinicSystem.Core.Models;

public class Patient
{
    public int PatientID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Contact { get; set; } // optional contact info, e.g., email or secondary phone
    public string? Address { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public decimal ConsultationFee { get; set; }
    public decimal Discount { get; set; }

    public decimal TotalBill => Math.Max(ConsultationFee - Discount, 0);

    /// <summary>Derived display string for lists/overlays.</summary>
    public string DisplayText => $"{Name} — {Phone ?? "No contact"} — Age: {Age?.ToString() ?? "N/A"}";
}
