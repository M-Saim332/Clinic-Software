using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class MedicineReturnViewModel : ViewModelBase
{
    private readonly ReturnRepository _returnRepo;
    private readonly SaleRepository _saleRepo;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Sale> _foundSales = new();

    [ObservableProperty]
    private Sale? _selectedSale;

    [ObservableProperty]
    private ObservableCollection<ReturnableItemViewModel> _returnableItems = new();

    [ObservableProperty]
    private decimal _totalRefundAmount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    public MedicineReturnViewModel(ReturnRepository returnRepo, SaleRepository saleRepo)
    {
        _returnRepo = returnRepo;
        _saleRepo = saleRepo;
    }

    [RelayCommand]
    private void SearchSales()
    {
        StatusMessage = "Searching...";
        try
        {
            var sales = _saleRepo.GetAll();
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLowerInvariant();
                sales = sales.Where(s => 
                    s.InvoiceNumber.ToLowerInvariant().Contains(q) || 
                    (s.PatientName != null && s.PatientName.ToLowerInvariant().Contains(q)));
            }
            
            FoundSales = new ObservableCollection<Sale>(sales);
            SelectedSale = null;
            ReturnableItems.Clear();
            CalculateTotalRefund();
            StatusMessage = $"Found {FoundSales.Count} sales.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
    }

    partial void OnSelectedSaleChanged(Sale? value)
    {
        ReturnableItems.Clear();
        CalculateTotalRefund();
        
        if (value == null) return;
        
        try
        {
            var sale = _saleRepo.GetByIdWithItems(value.SaleID);
            if (sale == null) return;
            
            var pastReturns = _returnRepo.GetBySaleId(sale.SaleID).ToList();
            
            foreach (var item in sale.Items)
            {
                var alreadyReturned = pastReturns.Where(r => r.MedicineId == item.MedicineID).Sum(r => r.QuantityReturned);
                var returnable = item.Quantity - alreadyReturned;
                
                if (returnable > 0)
                {
                    var vm = new ReturnableItemViewModel(item, returnable);
                    vm.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(ReturnableItemViewModel.QuantityToReturn))
                        {
                            CalculateTotalRefund();
                        }
                    };
                    ReturnableItems.Add(vm);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load sale details: {ex.Message}";
        }
    }

    private void CalculateTotalRefund()
    {
        TotalRefundAmount = ReturnableItems.Sum(x => x.RefundAmount);
    }

    private bool CanConfirmReturn()
    {
        return !IsProcessing && ReturnableItems.Any(x => x.QuantityToReturn > 0);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmReturn))]
    private async Task ConfirmReturnAsync()
    {
        IsProcessing = true;
        ConfirmReturnCommand.NotifyCanExecuteChanged();
        StatusMessage = "Processing returns...";
        
        try
        {
            await Task.Run(() => 
            {
                var itemsToReturn = ReturnableItems.Where(x => x.QuantityToReturn > 0).ToList();
                foreach (var item in itemsToReturn)
                {
                    if (item.QuantityToReturn > item.MaxReturnable)
                    {
                        throw new InvalidOperationException($"Cannot return {item.QuantityToReturn} of {item.Item.MedicineName} (Max: {item.MaxReturnable})");
                    }
                    if (string.IsNullOrWhiteSpace(item.Reason))
                    {
                        throw new InvalidOperationException($"Please specify a reason for returning {item.Item.MedicineName}.");
                    }
                }
                
                foreach (var item in itemsToReturn)
                {
                    var ret = new MedicineReturn
                    {
                        SaleId = SelectedSale!.SaleID,
                        MedicineId = item.Item.MedicineID,
                        PatientId = SelectedSale.PatientID,
                        QuantityReturned = item.QuantityToReturn,
                        UnitPriceAtSale = item.Item.MedicinePrice, // actually should be price from SaleItem
                        RefundAmount = item.RefundAmount,
                        Reason = item.Reason,
                        ReturnDate = DateTime.Now,
                        ProcessedBy = CurrentUser?.UserID,
                        Status = "Completed"
                    };
                    _returnRepo.Insert(ret);
                }
            });
            
            StatusMessage = $"Successfully processed returns. Refund Total: {TotalRefundAmount:C}";
            SearchSales(); // Reset view
        }
        catch (Exception ex)
        {
            StatusMessage = $"Return failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            ConfirmReturnCommand.NotifyCanExecuteChanged();
        }
    }
}

public partial class ReturnableItemViewModel : ObservableObject
{
    public SaleItem Item { get; }
    public int MaxReturnable { get; }
    
    [ObservableProperty]
    private int _quantityToReturn;
    
    [ObservableProperty]
    private string _reason = "Patient Changed Mind";

    public List<string> Reasons { get; } = new() 
    {
        "Patient Changed Mind",
        "Expired",
        "Wrong Medicine",
        "Adverse Reaction",
        "Other"
    };

    public decimal RefundAmount => QuantityToReturn * Item.MedicinePrice;

    public ReturnableItemViewModel(SaleItem item, int maxReturnable)
    {
        Item = item;
        MaxReturnable = maxReturnable;
    }
    
    partial void OnQuantityToReturnChanged(int value)
    {
        if (value < 0) QuantityToReturn = 0;
        if (value > MaxReturnable) QuantityToReturn = MaxReturnable;
        OnPropertyChanged(nameof(RefundAmount));
    }
}
