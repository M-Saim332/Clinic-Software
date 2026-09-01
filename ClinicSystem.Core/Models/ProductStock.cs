namespace ClinicSystem.Core.Models;

public class ProductStock
{
    public int StockID { get; set; }
    public int ProductID { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int QuantityAvailable { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal MRP { get; set; }
    public bool IsArchived { get; set; }
    /// <summary>Filled by the active-batch query so MRP can be shown per piece.</summary>
    public int PiecesPerPack { get; set; } = 1;

    /// <summary>Label used when choosing one specific expiry batch in the UI.</summary>
    public decimal MRPPerPiece => MRP / Math.Max(1, PiecesPerPack);
    public string BatchDisplay => $"Exp: {ExpiryDate:MM/yyyy} | Qty: {QuantityAvailable:N0} pcs | Rs. {MRPPerPiece:N2}/pc";
}
