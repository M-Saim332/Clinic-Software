namespace ClinicSystem.Core.Models;

public class PrescriptionItem
{
    public int PrescriptionItemID { get; set; }
    public int PrescriptionID { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }
    public string? Dosage { get; set; }

    // Navigation
    public string? ProductName { get; set; }
}
