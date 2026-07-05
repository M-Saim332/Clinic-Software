namespace ClinicSystem.Core.Models;

public class Prescription
{
    public int PrescriptionID { get; set; }
    public int PatientID { get; set; }
    public int DoctorID { get; set; }
    public DateTime VisitDate { get; set; }
    public string? Diagnosis { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation (populated by joins in repo)
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }
    public List<PrescriptionItem> Items { get; set; } = new();
}
