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
    public string WorkflowStatus { get; set; } = "Draft";
    public DateTime? SentToPharmacyAt { get; set; }
    public DateTime? PrintedAt { get; set; }
    public DateTime? DispensedAt { get; set; }

    // Navigation (populated by joins in repo)
    public string? PatientName { get; set; }
    public int? PatientAge { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientPhone { get; set; }
    public string? DoctorName { get; set; }
    public List<PrescriptionItem> Items { get; set; } = new();

    public string WorkflowStatusLabel => WorkflowStatus switch
    {
        "SentToPharmacy" => "New",
        "Printed" => "Checked & Printed",
        "Dispensed" => "Medicines Given",
        _ => WorkflowStatus
    };

    public string SentTimeDisplay => SentToPharmacyAt?.ToString("dd MMM, h:mm tt") ?? VisitDate.ToString("dd MMM, h:mm tt");
}
