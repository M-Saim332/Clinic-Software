namespace ClinicSystem.Core.Models;

public class SaleItem
{
    public int SaleItemID { get; set; }
    public int SaleID { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal LineTotal { get; set; }

    // Join helper properties
    public string? ProductName { get; set; }
    public decimal ProductPrice { get; set; }

    // Derived properties for billing formula layer
    public decimal GrossLineAmount => Quantity * ProductPrice;
    public decimal DiscountedValue => GrossLineAmount * (Discount / 100);
    public decimal TaxableOverhead => (GrossLineAmount - DiscountedValue) * (Tax / 100);
    public decimal LineNetTotal => (GrossLineAmount - DiscountedValue) + TaxableOverhead;
}
