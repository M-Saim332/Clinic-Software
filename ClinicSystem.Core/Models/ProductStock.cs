namespace ClinicSystem.Core.Models;

public class ProductStock
{
    public int StockID { get; set; }
    public int ProductID { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int QuantityAvailable { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal MRP { get; set; }
}
