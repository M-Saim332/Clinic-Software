namespace ClinicSystem.Core.Models;

public class ReturnItem
{
    public int ReturnItemID { get; set; }
    public int ReturnId { get; set; }
    public int ProductId { get; set; }

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

    public string? Reason { get; set; }
    public decimal RefundAmount { get; set; }

    // Join helpers for UI
    public string? ProductName { get; set; }
    public string? ProductType { get; set; }
}
