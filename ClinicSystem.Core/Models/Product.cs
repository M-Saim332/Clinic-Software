namespace ClinicSystem.Core.Models;

public class Product
{
    public int ProductID { get; set; }
    public int PCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Barcode { get; set; }
    public int? CompanyID { get; set; }
    public string? CompanyName { get; set; }
    public int? SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public string? BatchNumber { get; set; }
    public string? Type { get; set; }
    public string? Packing { get; set; }
    public string? Category { get => Packing; set => Packing = value; }
    public string? Rack { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    /// <summary>Gross trade price per pack as entered manually or from latest purchase batch.</summary>
    public decimal Rate { get; set; }
    public decimal MRP { get => SellingPrice; set => SellingPrice = value; }
    public int PiecesPerUnit { get; set; } = 1;
    public int TabletsPerBox { get => PiecesPerUnit; set => PiecesPerUnit = value; }
    public int Stock { get; set; }
    public int MinimumStockLevel { get; set; } = 10;
    public bool IsReturnable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? LastStockUpdateDate { get; set; }

    public decimal PricePerTablet =>
        PiecesPerUnit > 0
            ? Math.Round(SellingPrice / PiecesPerUnit, 2, MidpointRounding.AwayFromZero)
            : SellingPrice;

    /// <summary>
    /// Estimated landed cost per piece using the stored Rate with a standard 15 % trade discount.
    /// Used for live margin preview on the product form when no PurchaseItems batch exists.
    /// Formula: Rate × (1 − 0.15) / PiecesPerUnit
    /// </summary>
    public decimal EstimatedLandedCostPerPiece =>
        PiecesPerUnit > 0 && Rate > 0
            ? Math.Round((Rate * 0.85m) / PiecesPerUnit, 4, MidpointRounding.AwayFromZero)
            : PurchasePrice;

    /// <summary>
    /// Estimated profit margin per piece = (SellingPrice / PiecesPerUnit) − EstimatedLandedCostPerPiece.
    /// </summary>
    public decimal EstimatedMarginPerPiece =>
        PricePerTablet - EstimatedLandedCostPerPiece;
    public int FullPacksInStock => PiecesPerUnit > 0 ? Stock / PiecesPerUnit : Stock;
    public int LoosePiecesInStock => PiecesPerUnit > 0 ? Stock % PiecesPerUnit : 0;
    public string StockBreakdown => PiecesPerUnit > 1
        ? $"{Stock} pieces ({FullPacksInStock} pack{(FullPacksInStock == 1 ? string.Empty : "s")} + {LoosePiecesInStock} piece{(LoosePiecesInStock == 1 ? string.Empty : "s")})"
        : $"{Stock} pieces";
    public string ProductCodeDisplay => PCode > 0 ? $"Code {PCode}" : $"ID {ProductID}";
    public string PrescriptionSearchDetail
    {
        get
        {
            var type = string.IsNullOrWhiteSpace(Type) ? "—" : Type;
            return $"{Name} — {type} | Price: Rs. {SellingPrice:N2} | Stock: {Stock} Pcs";
        }
    }

    // Alias properties used by XAML bindings in Reports view
    public int MinStock => MinimumStockLevel;
    public decimal Price => SellingPrice;
    public string? Manufacturer => CompanyName;

    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
    public int MinimumStockPieces => MinimumStockLevel * PiecesPerUnit;
    public bool IsLowStock => Stock <= MinimumStockPieces;
    public string StockStatus => IsExpired ? "EXPIRED" : IsLowStock ? "LOW" : "OK";
}
