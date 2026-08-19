namespace ClinicSystem.Core.Models;

public class PurchaseItem
{
    public int PurchaseItemID { get; set; }
    public int PurchaseID { get; set; }
    public int ProductID { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public int BonusQuantity { get; set; }
    public string PackageType { get; set; } = "Box";
    public int PackageQuantity { get; set; }
    public int UnitsPerPackage { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }

    // Join helper property
    public string? ProductName { get; set; }

    // Derived properties for billing formula layer
    public int TotalStockUnits => (PackageQuantity + BonusQuantity) * Math.Max(1, UnitsPerPackage);
    public decimal GrossLineAmount => PackageQuantity * PurchasePrice;
    public decimal DiscountedValue => GrossLineAmount * (Discount / 100);
    public decimal TaxableOverhead => (GrossLineAmount - DiscountedValue) * (Tax / 100);
    public decimal LineNetTotal => (GrossLineAmount - DiscountedValue) + TaxableOverhead;
}
