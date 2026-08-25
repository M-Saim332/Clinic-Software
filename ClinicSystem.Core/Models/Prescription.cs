namespace ClinicSystem.Core.Models;

public class Prescription
{
    public int PrescriptionID { get; set; }
    public int PatientID { get; set; }
    public int DoctorID { get; set; }
    public int? AppointmentID { get; set; }
    public int? PharmacistID { get; set; }
    public DateTime VisitDate { get; set; }
    /// <summary>Legacy – kept for DB/history compatibility. New workflow uses LabTests instead.</summary>
    public string? Diagnosis { get; set; }
    /// <summary>Legacy – kept for DB/history compatibility.</summary>
    public string? Notes { get; set; }
    /// <summary>Optional lab-test instructions added by the doctor during consultation.</summary>
    public string? LabTests { get; set; }
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

    public bool IsSentToPharmacy => WorkflowStatus == "SentToPharmacy";
    public bool IsPrinted => WorkflowStatus == "Printed";
    public bool IsDispensed => WorkflowStatus == "Dispensed";
    public bool IsPrintedOrDispensed => WorkflowStatus == "Printed" || WorkflowStatus == "Dispensed";

    public string SentTimeDisplay => SentToPharmacyAt?.ToString("dd MMM, h:mm tt") ?? VisitDate.ToString("dd MMM, h:mm tt");
    public string VisitDateDisplay => VisitDate.ToString("dd MMM yyyy, h:mm tt");
    public string PrimaryReasonDisplay => !string.IsNullOrWhiteSpace(Diagnosis)
        ? Diagnosis
        : !string.IsNullOrWhiteSpace(Notes) ? Notes : "Not recorded";
    public string DoctorNotesDisplay => !string.IsNullOrWhiteSpace(Notes) ? Notes : "No attending notes recorded.";
    public string MedicinesSummary => Items.Count == 0
        ? "No medicines recorded."
        : string.Join(", ", Items.Select(i =>
        {
            var name = string.IsNullOrWhiteSpace(i.ProductName) ? "Medicine" : i.ProductName;
            var dosage = string.IsNullOrWhiteSpace(i.Dosage) ? string.Empty : $" - {i.Dosage}";
            return $"{name} x{i.Quantity}{dosage}";
        }));
}
