using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class InvoiceViewModel : ViewModelBase
{
    private readonly SaleRepository     _saleRepo;
    private readonly SettingsRepository _settingsRepo;
    public Action? RequestGoBack { get; set; }
    public Action? RequestPrint  { get; set; }

    public InvoiceViewModel(SaleRepository saleRepo, SettingsRepository settingsRepo)
    {
        _saleRepo     = saleRepo;
        _settingsRepo = settingsRepo;
    }

    [ObservableProperty] private Sale?   _saleData;
    [ObservableProperty] private ObservableCollection<SaleItem> _lineItems = new();
    [ObservableProperty] private string  _statusMessage = string.Empty;

    // Clinic branding — loaded from Settings
    [ObservableProperty] private string _clinicName    = "Clinic Management";
    [ObservableProperty] private string _clinicAddress = string.Empty;
    [ObservableProperty] private string _clinicPhone   = string.Empty;

    public decimal DocumentSubTotal => LineItems.Sum(x => x.GrossLineAmount);
    public int DocumentTotalItems => LineItems.Count;
    public int DocumentTotalQuantity => LineItems.Sum(x => x.Quantity);
    public decimal DocumentGrossAmount => LineItems.Sum(x => x.GrossLineAmount);
    public decimal DocumentDiscountAmount => LineItems.Sum(x => x.DiscountAmount);
    public decimal DocumentAdvanceWHTax => LineItems.Sum(x => x.AdvTaxAmount);
    public decimal DocumentTaxAmount => LineItems.Sum(x => x.TaxAmount) + (SaleData?.GrandTotal - LineItems.Sum(x => x.LineNetTotal) ?? 0);
    public decimal DocumentCNValue => 0;
    public decimal DocumentAdjustmentsTotal =>
        LineItems.Sum(x => x.Tax - x.Discount) + (SaleData?.GrandTotal - LineItems.Sum(x => x.LineNetTotal) ?? 0);
    public bool HasAdjustments => DocumentDiscountAmount > 0 || DocumentTaxAmount > 0;
    public decimal DocumentGrandTotal => SaleData?.GrandTotal ?? LineItems.Sum(x => x.LineNetTotal);
    public string DocumentStatus => SaleData?.IsPosted == true ? "POSTED" : "DRAFT";
    public string DocumentGeneratedDateDisplay => DateTime.Now.ToString("dd MMM yyyy hh:mm tt");

    public void LoadInvoice(Sale sale)
    {
        SaleData = sale;
        _ = LoadItemsAsync(sale.SaleID);
        _ = LoadClinicSettingsAsync();
    }

    private async Task LoadClinicSettingsAsync()
    {
        try
        {
            var dict = await Task.Run(() => _settingsRepo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (dict.TryGetValue("ClinicName",    out var n)) ClinicName    = n;
                if (dict.TryGetValue("ClinicAddress", out var a)) ClinicAddress = a;
                if (dict.TryGetValue("ClinicPhone",   out var p)) ClinicPhone   = p;
            });
        }
        catch { /* silently ignore — use defaults */ }
    }

    private async Task LoadItemsAsync(int saleId)
    {
        var saleWithItems = await Task.Run(() => _saleRepo.GetByIdWithItems(saleId));
        if (saleWithItems != null && saleWithItems.Items != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Force UI refresh by nulling first, then re-assigning the fresh complete data
                SaleData = null;
                SaleData = saleWithItems;
                LineItems = new ObservableCollection<SaleItem>(saleWithItems.Items);
                NotifyDocumentTotals();
            });
        }
    }

    partial void OnSaleDataChanged(Sale? value) => NotifyDocumentTotals();
    partial void OnLineItemsChanged(ObservableCollection<SaleItem> value) => NotifyDocumentTotals();

    private void NotifyDocumentTotals()
    {
        OnPropertyChanged(nameof(DocumentSubTotal));
        OnPropertyChanged(nameof(DocumentTotalItems));
        OnPropertyChanged(nameof(DocumentTotalQuantity));
        OnPropertyChanged(nameof(DocumentGrossAmount));
        OnPropertyChanged(nameof(DocumentDiscountAmount));
        OnPropertyChanged(nameof(DocumentAdvanceWHTax));
        OnPropertyChanged(nameof(DocumentTaxAmount));
        OnPropertyChanged(nameof(DocumentCNValue));
        OnPropertyChanged(nameof(DocumentAdjustmentsTotal));
        OnPropertyChanged(nameof(DocumentGrandTotal));
        OnPropertyChanged(nameof(DocumentStatus));
    }

    [RelayCommand]
    private void GoBack()
    {
        RequestGoBack?.Invoke();
    }

    [RelayCommand]
    private void Print()
    {
        RequestPrint?.Invoke();
        StatusMessage = "Sent to printer!";
    }

    [RelayCommand]
    private void EmailDocument()
    {
        StatusMessage = "Email document action is available from the invoice preview.";
    }
}
