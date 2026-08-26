namespace ClinicSystem.Core.Models;

public class SaleItem
{
    public int SaleItemID { get; set; }
    public int SerialNumber { get; set; }
    public int SaleID { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }
    public string UnitTypeSold { get; set; } = "Pieces";
    public int StockQuantity { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal LineTotal { get; set; }

    // Join helper properties
    public string? ProductName { get; set; }
    public decimal ProductPrice { get; set; }
    public decimal UnitPrice { get => ProductPrice; set => ProductPrice = value; }

    // Derived properties for billing formula layer
    public decimal GrossLineAmount => Quantity * ProductPrice;
    public decimal LineDiscountAmount => GrossLineAmount * (Discount / 100m);
    public decimal LineNetTotal => Math.Max(0, GrossLineAmount - LineDiscountAmount + Tax);
    public decimal InvoiceItemTotal => Math.Max(0, GrossLineAmount - LineDiscountAmount);
    public string BatchExpiryDisplay => "N/A";
    public string RateDisplay => $"Rs. {ProductPrice:N2} / {UnitTypeSold}";
    public string TaxDiscountDisplay => $"Tax Rs. {Tax:N2} / Disc {Discount:N2}%";
    public string ItemCodeDisplay => SerialNumber > 0 ? SerialNumber.ToString() : "-";
    public string BatchNumberDisplay => "-";
    public string ExpiryDateDisplay => "-";
    public int BonusQuantity => 0;
    public decimal DiscountPercent => Discount;
    public decimal DiscountAmount => LineDiscountAmount;
    public decimal AdvTaxAmount => 0;
    public decimal TaxAmount => Tax;
    public decimal NetAmount => InvoiceItemTotal;
}
