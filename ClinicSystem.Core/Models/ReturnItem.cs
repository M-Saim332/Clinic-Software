namespace ClinicSystem.Core.Models;

public class ReturnItem
{
    public int ReturnItemID { get; set; }
    public int ReturnId { get; set; }
    public int ProductId { get; set; }
    /// <summary>Specific ProductStock batch selected for this return.</summary>
    public int? StockID { get; set; }

    /// <summary>
    /// Always stored in Pieces (the piece-converted quantity).
    /// For Patient returns: same as EnteredQuantity.
    /// For Supplier returns: EnteredQuantity × PiecesPerUnit.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>What the user actually typed — Pieces for Patient, Packs for Supplier.</summary>
    public int EnteredQuantity { get; set; }

    /// <summary>"Pieces" or "Packs"</summary>
    public string UnitType { get; set; } = "Pieces";
    /// <summary>The actual rate used for this transaction: TP/pack or MRP/piece.</summary>
    public decimal UnitPrice { get; set; }
    public int PiecesPerPack { get; set; } = 1;

    public string? Reason { get; set; }
    public decimal RefundAmount { get; set; }

    /// <summary>Display quantity in the unit the operator selected.</summary>
    public string QuantityWithUnit => $"{EnteredQuantity:N0} {(UnitType == "Packs" ? "Packs" : "Pcs")}";

    // Join helpers for UI
    public string? ProductName { get; set; }
    public string? ProductType { get; set; }
}
