namespace ClinicSystem.Core.Models;

public class Medicine
{
    public int MedicineID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Formula { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; } = 10;
    public DateTime? ExpiryDate { get; set; }
    public decimal BuyingPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal Price { get => SellingPrice; set => SellingPrice = value; }
    public string? Manufacturer { get; set; }
    public string? Company { get; set; }
    public string? SupplierName { get; set; }
    public string? Category { get; set; }
    public DateTime? BuyingDate { get; set; }
    public int UnitsBought { get; set; }
    public string StockType { get; set; } = "Bought";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.Today;
    public bool IsLowStock => Stock <= MinStock;
    public string StockStatus => IsExpired ? "EXPIRED" : IsLowStock ? "LOW" : "OK";
    public int UnitsSold => Math.Max(UnitsBought - Stock, 0);
    public decimal ProfitPerUnit => SellingPrice - BuyingPrice;
    public decimal InventoryCost => UnitsBought * BuyingPrice;
    public decimal InventoryValue => Stock * SellingPrice;
    public decimal Revenue => UnitsSold * SellingPrice;
    public decimal Profit => UnitsSold * ProfitPerUnit;
}
