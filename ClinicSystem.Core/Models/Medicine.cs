namespace ClinicSystem.Core.Models;

public class Medicine
{
    public int MedicineID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public int? CompanyID { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int Stock { get; set; }
    public int MinimumStockLevel { get; set; } = 10;
    public bool IsReturnable { get; set; } = true;

    // Join helper property
    public string? CompanyName { get; set; }

    // Alias properties used by XAML bindings in Reports view
    public int MinStock => MinimumStockLevel;
    public decimal Price => SellingPrice;
    public string? Manufacturer => CompanyName;

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.Today;
    public bool IsLowStock => Stock <= MinimumStockLevel;
    public string StockStatus => IsExpired ? "EXPIRED" : IsLowStock ? "LOW" : "OK";
}
