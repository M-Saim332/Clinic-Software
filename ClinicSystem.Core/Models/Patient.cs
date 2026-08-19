namespace ClinicSystem.Core.Models;

public class Patient
{
    public bool IsActive { get; set; } = true;
    public int PatientID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Contact { get; set; }
    public string? CNIC { get; set; }
    public string? Address { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public decimal ConsultationFee { get; set; }
    public decimal? Discount { get; set; }
    public DateTime? NextAppointmentDate { get; set; }
    public TimeSpan? NextAppointmentTime { get; set; }
    
    // Visit Status Tracking
    public string? VisitStatus { get; set; }
    public DateTime? LastVisitDate { get; set; }

    public decimal TotalBill => Math.Max(ConsultationFee - (ConsultationFee * (Discount ?? 0m) / 100m), 0);

    public string DisplayText => $"{Name} — {Phone ?? "No contact"} — Age: {Age?.ToString() ?? "N/A"}";

    /// <summary>Appointment time formatted as 12-hour AM/PM, e.g. "3:30 PM". Returns "—" when no time is set.</summary>
    public string AppointmentTimeDisplay =>
        NextAppointmentTime.HasValue
            ? DateTime.Today.Add(NextAppointmentTime.Value).ToString("h:mm tt")
            : "—";
}
