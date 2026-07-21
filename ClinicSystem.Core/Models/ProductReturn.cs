namespace ClinicSystem.Core.Models;
using System;

public class ProductReturn
{
    public int ReturnId { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ReturnType { get; set; } = "Patient Return"; // Patient Return or Supplier Return
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public int? SupplierId { get; set; }
    public int? SaleId { get; set; }
    public decimal RefundAmount { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Join helpers for UI
    public string? ProductName { get; set; }
    public string? CreatedByName { get; set; }
}
