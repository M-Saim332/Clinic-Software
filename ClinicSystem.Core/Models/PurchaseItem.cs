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
    public decimal CompanySalesTax { get; set; }

    // Join helper property
    public string? ProductName { get; set; }

    // Derived properties for billing formula layer
    public int TotalStockUnits => PackageQuantity + BonusQuantity;
    public decimal EffectiveRate => PurchasePrice;
    public decimal UnitMRP => PackMRP;
    public decimal GrossLineAmount => PackageQuantity * PurchasePrice;
    public decimal DiscountedValue => GrossLineAmount * (Discount / 100m);
    public decimal ExtraDiscountedValue => GrossLineAmount * (ExtraDiscount / 100m);
    public decimal SubTotal => Math.Max(0, GrossLineAmount - DiscountedValue - ExtraDiscountedValue);
    public decimal AdvanceTaxAmount => SubTotal * (ATax / 100m);
    public decimal CompanySalesTaxAmount => SubTotal * (CompanySalesTax / 100m);
    public decimal TaxableOverhead => AdvanceTaxAmount + CompanySalesTaxAmount;
    public decimal LineNetTotal => SubTotal + TaxableOverhead;
    public string BatchExpiryDisplay =>
        $"{(string.IsNullOrWhiteSpace(BatchNumber) ? "N/A" : BatchNumber)} / {(ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd MMM yyyy") : "N/A")}";
    public string RateMrpDisplay => $"T.P Rs. {PurchasePrice:N2} / MRP Rs. {PackMRP:N2}";
    public string TaxDiscountDisplay => $"Tax {ATax + CompanySalesTax:N2}% / Disc {Discount + ExtraDiscount:N2}%";
}
