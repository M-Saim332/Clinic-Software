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
    public decimal PackMRP { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal ExtraDiscount { get; set; }
    public decimal ATax { get; set; }

    // Join helper property
    public string? ProductName { get; set; }

    // Derived properties for billing formula layer
    public int TotalStockUnits => (PackageQuantity + BonusQuantity) * Math.Max(1, UnitsPerPackage);
    public decimal EffectiveRate => Math.Round(PurchasePrice * 0.85m, 2, MidpointRounding.AwayFromZero);
    public decimal UnitMRP => UnitsPerPackage > 0
        ? Math.Round(PackMRP / UnitsPerPackage, 2, MidpointRounding.AwayFromZero)
        : PackMRP;
    public decimal GrossLineAmount => PackageQuantity * EffectiveRate;
    public decimal DiscountedValue => GrossLineAmount * (Discount / 100m);
    public decimal ExtraDiscountedValue => GrossLineAmount * (ExtraDiscount / 100m);
    public decimal TaxableOverhead => GrossLineAmount * (ATax / 100m);
    public decimal LineNetTotal => Math.Max(0, GrossLineAmount - DiscountedValue - ExtraDiscountedValue + TaxableOverhead);
}
