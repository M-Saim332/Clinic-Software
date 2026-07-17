namespace ClinicSystem.Core.Models;

public class Product
{
    public int ProductID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Barcode { get; set; }
    public int? CompanyID { get; set; }
    public string? CompanyName { get; set; }
    public int? SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public string? BatchNumber { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? Rack { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int Stock { get; set; }
    public int MinimumStockLevel { get; set; } = 10;
    public bool IsReturnable { get; set; } = true;

    // Alias properties used by XAML bindings in Reports view
    public int MinStock => MinimumStockLevel;
    public decimal Price => SellingPrice;
    public string? Manufacturer => CompanyName;

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.Today;
    public bool IsLowStock => Stock <= MinimumStockLevel;
    public string StockStatus => IsExpired ? "EXPIRED" : IsLowStock ? "LOW" : "OK";
}
