namespace ClinicSystem.Core.Models;

public class Medicine
{
    public int MedicineID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int MinStock { get; set; } = 10;
    public DateTime? ExpiryDate { get; set; }
    public decimal Price { get; set; }
    public string? Manufacturer { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.Today;
    public bool IsLowStock => Stock <= MinStock;
    public string StockStatus => IsExpired ? "EXPIRED" : IsLowStock ? "LOW" : "OK";
}
