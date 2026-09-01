namespace ClinicSystem.Core.Models;

public class ProductStock
{
    public int StockID { get; set; }
    public int ProductID { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int QuantityAvailable { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal MRP { get; set; }

    /// <summary>Label used when choosing one specific expiry batch in the UI.</summary>
    public string BatchDisplay => $"Expires {ExpiryDate:dd MMM yyyy} — {QuantityAvailable:N0} pieces available";
}
