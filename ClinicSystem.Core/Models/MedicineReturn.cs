namespace ClinicSystem.Core.Models;

public class MedicineReturn
{
    public int ReturnId { get; set; }
    public int SaleId { get; set; }
    public int MedicineId { get; set; }
    public int? PatientId { get; set; }
    public int QuantityReturned { get; set; }
    public decimal UnitPriceAtSale { get; set; }
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public int? ProcessedBy { get; set; }
    public string Status { get; set; } = "Completed";

    // Join helper properties for UI
    public string? MedicineName { get; set; }
    public string? PatientName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ProcessedByName { get; set; }
}
