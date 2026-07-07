namespace ClinicSystem.Core.Models;

public class Appointment
{
    public int AppointmentID { get; set; }
    public int PatientID { get; set; }
    public int DoctorID { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan AppointmentTime { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? LinkedVisitID { get; set; }

    // Join helper properties
    public string? PatientName { get; set; }
    public string? PatientPhone { get; set; }
    public string? DoctorName { get; set; }
}
