namespace ClinicSystem.Core.Models;

public class Purchase
{
    public int PurchaseID { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; } = DateTime.Now;
    public int? SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }

    public List<PurchaseItem> Items { get; set; } = new();
}
