namespace ClinicSystem.Core.Models;

public class ProductReturn
{
    public int ReturnId { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public int? PatientId { get; set; }
    public int QuantityReturned { get; set; }
    public decimal UnitPriceAtSale { get; set; }
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public int? ProcessedBy { get; set; }
    public string Status { get; set; } = "Completed";

    // Join helper properties for UI
    public string? ProductName { get; set; }
    public string? PatientName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ProcessedByName { get; set; }
}
