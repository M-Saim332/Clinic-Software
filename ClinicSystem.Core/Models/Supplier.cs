namespace ClinicSystem.Core.Models;

public class Supplier
{
    public int SupplierID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
