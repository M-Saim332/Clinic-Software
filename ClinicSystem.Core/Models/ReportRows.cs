namespace ClinicSystem.Core.Models;

public class SaleInvoiceSummaryRow
{
    public int SaleID { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? PatientName { get; set; }
    public string? ReceptionistName { get; set; }
    public decimal GrandTotal { get; set; }
}

public class BestSellingProductRow
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int PiecesSold { get; set; }
    public decimal Revenue { get; set; }
}

public class CompanySaleRow
{
    public int? CompanyID { get; set; }
    public string CompanyName { get; set; } = "Unassigned";
    public int PiecesSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Revenue - Cost;
}
