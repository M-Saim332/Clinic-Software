namespace ClinicSystem.Core.Models;

public class PurchaseItem
{
    public int PurchaseItemID { get; set; }
    public int PurchaseID { get; set; }
    public int ProductID { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }

    // Join helper property
    public string? ProductName { get; set; }

    // Derived properties for billing formula layer
    public decimal GrossLineAmount => Quantity * PurchasePrice;
    public decimal DiscountedValue => GrossLineAmount * (Discount / 100);
    public decimal TaxableOverhead => (GrossLineAmount - DiscountedValue) * (Tax / 100);
    public decimal LineNetTotal => (GrossLineAmount - DiscountedValue) + TaxableOverhead;
}
