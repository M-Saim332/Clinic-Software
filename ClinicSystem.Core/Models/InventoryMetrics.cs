namespace ClinicSystem.Core.Models;

/// <summary>Batch-level inventory counters used by every inventory summary.</summary>
public sealed class InventoryMetrics
{
    public int TotalStockItems { get; set; }
    public int LowStockItems { get; set; }
    public int OutOfStockItems { get; set; }
    public int ExpiredBatches { get; set; }
}
