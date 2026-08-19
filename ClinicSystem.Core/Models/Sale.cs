namespace ClinicSystem.Core.Models;

public class Sale
{
    public int SaleID { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; } = DateTime.Now;
    public int? PatientID { get; set; }
    public decimal ConsultationFee { get; set; }
    public decimal GrandTotal { get; set; }
    public string? PaymentMethod { get; set; }
    public bool IsPosted { get; set; }
    public int? ReceptionistId { get; set; }
    public string? ReceptionistName { get; set; }
    public bool IsActive { get; set; } = true;

    // Join helper properties
    public string? PatientName { get; set; }
    public List<SaleItem> Items { get; set; } = new();
}
