namespace ClinicSystem.Core.Models;

public class PrescriptionItem
{
    public int PrescriptionItemID { get; set; }
    public int PrescriptionID { get; set; }
    public int MedicineID { get; set; }
    public int Quantity { get; set; }
    public string? Dosage { get; set; }

    // Navigation
    public string? MedicineName { get; set; }
}
