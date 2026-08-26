using ClinicSystem.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Returns;

/// <summary>
/// Represents one medicine-selection row in the Return form.
/// Each row binds to a SearchableComboBox for medicine and a NumericUpDown for quantity.
/// </summary>
public partial class ReturnRow : ObservableObject
{
    private readonly ProcessReturnViewModel _parent;

    public ReturnRow(ProcessReturnViewModel parent)
    {
        _parent = parent;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StockInfo))]
    [NotifyPropertyChangedFor(nameof(MaxQuantity))]
    [NotifyPropertyChangedFor(nameof(EnteredQuantity))]
    private Product? _selectedProduct;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PiecesQuantity))]
    [NotifyPropertyChangedFor(nameof(RefundAmount))]
    private int _enteredQuantity = 1;

    // Called by the parent VM when return type changes — refreshes labels/limits
    public void RefreshReturnType()
    {
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(PiecesQuantity));
        OnPropertyChanged(nameof(RefundAmount));
        OnPropertyChanged(nameof(MaxQuantity));
        OnPropertyChanged(nameof(StockInfo));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        // Reset quantity on medicine change
        EnteredQuantity = 1;
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(StockInfo));
        OnPropertyChanged(nameof(MaxQuantity));
        OnPropertyChanged(nameof(PiecesQuantity));
        OnPropertyChanged(nameof(RefundAmount));
    }

    partial void OnEnteredQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(PiecesQuantity));
        OnPropertyChanged(nameof(RefundAmount));
    }

    // ── Computed properties ────────────────────────────────────────────────

    /// <summary>"Pieces" for Patient returns, "Packs" for Supplier returns.</summary>
    public string UnitLabel => _parent.IsPatientReturn ? "Pieces" : "Packs";

    /// <summary>Quantity converted to individual pieces for stock/refund math.</summary>
    public int PiecesQuantity
    {
        get
        {
            if (SelectedProduct == null) return 0;
            if (_parent.IsPatientReturn)
                return EnteredQuantity;
            var ppu = SelectedProduct.PiecesPerUnit < 1 ? 1 : SelectedProduct.PiecesPerUnit;
            return EnteredQuantity * ppu;
        }
    }

    /// <summary>Refund amount for this row.</summary>
    public decimal RefundAmount
    {
        get
        {
            if (SelectedProduct == null) return 0;
            var unitPrice = _parent.IsPatientReturn
                ? SelectedProduct.SellingPrice
                : SelectedProduct.PurchasePrice;
            if (_parent.IsPatientReturn)
                return EnteredQuantity * (SelectedProduct.PiecesPerUnit > 0
                    ? Math.Round(unitPrice / SelectedProduct.PiecesPerUnit, 2, MidpointRounding.AwayFromZero)
                    : unitPrice);
            // Supplier: entered Packs × PurchasePrice per pack
            return EnteredQuantity * unitPrice;
        }
    }

    /// <summary>Maximum allowed entered quantity based on available stock.</summary>
    public int MaxQuantity
    {
        get
        {
            if (SelectedProduct == null) return 9999;
            if (_parent.IsPatientReturn) return SelectedProduct.Stock > 0 ? SelectedProduct.Stock : 9999;
            // Supplier: max packs = Stock / PiecesPerUnit
            var ppu = SelectedProduct.PiecesPerUnit < 1 ? 1 : SelectedProduct.PiecesPerUnit;
            return SelectedProduct.Stock > 0 ? Math.Max(1, SelectedProduct.Stock / ppu) : 9999;
        }
    }

    /// <summary>Human-readable stock info displayed under the medicine selector.</summary>
    public string StockInfo
    {
        get
        {
            if (SelectedProduct == null) return string.Empty;
            return _parent.IsPatientReturn
                ? $"Stock: {SelectedProduct.Stock} pcs | Sell Rs.{SelectedProduct.SellingPrice:N2}"
                : $"Stock: {SelectedProduct.Stock} pcs ({SelectedProduct.FullPacksInStock} packs) | Buy Rs.{SelectedProduct.PurchasePrice:N2}";
        }
    }

    /// <summary>True when this row has a product selected and quantity > 0.</summary>
    public bool IsValid => SelectedProduct != null && EnteredQuantity > 0;

    /// <summary>Validation error for this row, or empty string if valid.</summary>
    public string ValidationError
    {
        get
        {
            if (SelectedProduct == null) return "Select a medicine.";
            if (EnteredQuantity <= 0) return "Quantity must be > 0.";
            if (_parent.IsPatientReturn && PiecesQuantity > SelectedProduct.Stock)
                return $"Only {SelectedProduct.Stock} pieces in stock.";
            if (!_parent.IsPatientReturn)
            {
                var ppu = SelectedProduct.PiecesPerUnit < 1 ? 1 : SelectedProduct.PiecesPerUnit;
                var maxPacks = SelectedProduct.Stock / ppu;
                if (EnteredQuantity > maxPacks)
                    return $"Only {maxPacks} pack(s) in stock ({SelectedProduct.Stock} pcs).";
            }
            return string.Empty;
        }
    }
}
