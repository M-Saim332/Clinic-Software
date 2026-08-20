namespace ClinicSystem.Core.Models;
using System;

public class ProductReturn
{
    public int ReturnId { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string UnitType { get; set; } = "Pieces";
    public int StockQuantity { get; set; }
    public string ReturnType { get; set; } = "Patient Return"; // Patient Return or Supplier Return
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public int? SupplierId { get; set; }
    public int? SaleId { get; set; }
    public decimal RefundAmount { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    public List<ReturnItem> Items { get; set; } = new();

    // Join helpers for UI
    public string? ProductName { get; set; }
    public string? CreatedByName { get; set; }
    public string? PatientName { get; set; }
    public string? SupplierName { get; set; }
    public string CounterpartyName => PatientName ?? SupplierName ?? string.Empty;
    public string ProductSummary => Items.Count > 1 ? $"{Items[0].ProductName} +{Items.Count - 1} more" : ProductName ?? string.Empty;
}
