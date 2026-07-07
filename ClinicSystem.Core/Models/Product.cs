namespace ClinicSystem.Core.Models;

public class Product
{
    public int ProductID { get; set; }
    public int? CompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PurchaseRate { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal Tax { get; set; }
    public int StockQuantity { get; set; }

    // Join helper property
    public string? CompanyName { get; set; }
}
