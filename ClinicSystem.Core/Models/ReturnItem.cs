namespace ClinicSystem.Core.Models;

public class ReturnItem
{
    public int ReturnItemID { get; set; }
    public int ReturnId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public decimal RefundAmount { get; set; }

    public string? ProductName { get; set; }
    public string? ProductType { get; set; }
}
