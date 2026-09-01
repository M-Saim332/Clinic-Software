namespace ClinicSystem.Core.Models;

public class SaleItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public int SaleItemID { get; set; }
    public int SerialNumber { get; set; }
    public int SaleID { get; set; }
    public int ProductID { get; set; }
    public int? StockID { get; set; }
    public DateTime? BatchExpiryDate { get; set; }
    public int PiecesPerUnit { get; set; } = 1;
    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            RefreshUnitPricingAndStockQuantity();
            OnPropertyChanged(nameof(Quantity));
        }
    }
    private string _unitTypeSold = "Piece";
    public string UnitTypeSold
    {
        get => _unitTypeSold;
        set
        {
            if (_unitTypeSold == value) return;
            _unitTypeSold = value;
            RefreshUnitPricingAndStockQuantity();
            OnPropertyChanged(nameof(UnitTypeSold));
        }
    }
    public int StockQuantity { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal LineTotal { get; set; }

    // Join helper properties
    public string? ProductName { get; set; }
    private decimal _productPrice;
    public decimal ProductPrice
    {
        get => _productPrice;
        set
        {
            if (_productPrice == value) return;
            _productPrice = value;
            OnPropertyChanged(nameof(ProductPrice));
            OnPropertyChanged(nameof(UnitPrice));
            OnPropertyChanged(nameof(GrossLineAmount));
            OnPropertyChanged(nameof(LineDiscountAmount));
            OnPropertyChanged(nameof(LineNetTotal));
            OnPropertyChanged(nameof(InvoiceItemTotal));
        }
    }
    public decimal UnitPrice { get => ProductPrice; set => ProductPrice = value; }

    public List<ProductStock> AvailableStockBatches { get; set; } = new();
    private ProductStock? _selectedStockBatch;
    public ProductStock? SelectedStockBatch
    {
        get => _selectedStockBatch;
        set
        {
            if (_selectedStockBatch == value) return;
            _selectedStockBatch = value;
            StockID = value?.StockID;
            RefreshUnitPricingAndStockQuantity();
            OnPropertyChanged(nameof(SelectedStockBatch));
            OnPropertyChanged(nameof(StockID));
            OnPropertyChanged(nameof(BatchExpiryDisplay));
        }
    }

    // Derived properties for billing formula layer
    public decimal GrossLineAmount => Math.Round(Quantity * ProductPrice, 2, MidpointRounding.AwayFromZero);
    public decimal LineDiscountAmount => Math.Round(GrossLineAmount * (Discount / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal LineNetTotal => Math.Max(0m, Math.Round(GrossLineAmount - LineDiscountAmount + Tax, 2, MidpointRounding.AwayFromZero));
    public decimal InvoiceItemTotal => Math.Max(0m, Math.Round(GrossLineAmount - LineDiscountAmount, 2, MidpointRounding.AwayFromZero));
    public string BatchExpiryDisplay => (SelectedStockBatch?.ExpiryDate ?? BatchExpiryDate)?.ToString("dd MMM yyyy") ?? "N/A";
    public string RateDisplay => $"Rs. {ProductPrice:N2} / {UnitTypeSold}";
    public string TaxDiscountDisplay => $"Tax Rs. {Tax:N2} / Disc {Discount:N2}%";
    public string ItemCodeDisplay => SerialNumber > 0 ? SerialNumber.ToString() : "-";
    public string BatchNumberDisplay => "-";
    public string ExpiryDateDisplay => "-";
    public int BonusQuantity => 0;
    public decimal DiscountPercent => Discount;
    public decimal DiscountAmount => LineDiscountAmount;
    public decimal AdvTaxAmount => 0;
    public decimal TaxAmount => Tax;
    public decimal NetAmount => LineNetTotal;

    // POS sells and prices each line in individual pieces.  Package sizes are
    // used only to derive MRP per piece and never to inflate a line total.
    public List<string> SaleUnitTypes { get; } = ["Piece"];

    private void RefreshUnitPricingAndStockQuantity()
    {
        var piecesPerPack = Math.Max(1, PiecesPerUnit);
        StockQuantity = Quantity;
        if (SelectedStockBatch != null)
            ProductPrice = SelectedStockBatch.MRP / piecesPerPack;
        OnPropertyChanged(nameof(StockQuantity));
        OnPropertyChanged(nameof(GrossLineAmount));
        OnPropertyChanged(nameof(LineDiscountAmount));
        OnPropertyChanged(nameof(LineNetTotal));
        OnPropertyChanged(nameof(InvoiceItemTotal));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
