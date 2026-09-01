namespace ClinicSystem.Core.Models;

/// <summary>One independently manageable inventory row for one product expiry batch.</summary>
public class ProductStockBatchDto
{
    public int ProductID { get; set; }
    public int StockID { get; set; }
    public int PCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public int? CompanyID { get; set; }
    public string? Type { get; set; }
    public string? Packing { get; set; }
    public int PiecesPerUnit { get; set; } = 1;
    public int StockQuantity { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal RateTP { get; set; }
    public decimal MRP { get; set; }

    public decimal TradePricePerPiece => RateTP / Math.Max(1, PiecesPerUnit);
    public decimal MrpPerPiece => MRP / Math.Max(1, PiecesPerUnit);
    public decimal StockValueAtTradePrice => StockQuantity * TradePricePerPiece;

    public string StockDisplay => PiecesPerUnit > 1
        ? $"{StockQuantity:N0} pieces ({StockQuantity / PiecesPerUnit} pack{(StockQuantity / PiecesPerUnit == 1 ? string.Empty : "s")})"
        : $"{StockQuantity:N0} pieces";
    public bool IsExpired => ExpiryDate.Date < DateTime.Today;
    public bool IsExpiringIn60Days => ExpiryDate.Date >= DateTime.Today && ExpiryDate.Date <= DateTime.Today.AddDays(60);
    public bool IsExpiringIn6Months => ExpiryDate.Date > DateTime.Today.AddDays(60) && ExpiryDate.Date <= DateTime.Today.AddMonths(6);
    public bool IsExpirySafe => ExpiryDate.Date > DateTime.Today.AddMonths(6);
}
