namespace ClinicSystem.Core.Models;

public class Appointment
{
    public int AppointmentID { get; set; }
    public string AppointmentNo { get; set; } = string.Empty;
    public int? PatientID { get; set; }
    public string? PatientName { get; set; }
    public string? Phone { get; set; }
    public int DoctorID { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan AppointmentTime { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Remarks { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Join helper properties
    public string? PatientPhone { get; set; }
    public string? DoctorName { get; set; }

    public string DisplayPatientName =>
        !string.IsNullOrWhiteSpace(PatientName) ? PatientName
        : !string.IsNullOrWhiteSpace(PatientPhone) ? "Walk-in"
        : "—";
}
