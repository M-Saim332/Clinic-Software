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

    // Invoice branding is loaded from the active clinic/pharmacy profile.
    [ObservableProperty] private string _clinicName    = "DR ASIF PHARMA";
    [ObservableProperty] private string _clinicAddress = "Pirmahal, Near Imam Bargah";
    [ObservableProperty] private string _clinicPhone   = string.Empty;

    public decimal DocumentSubTotal => DocumentGrandTotal;
    public int DocumentTotalItems => LineItems.Count;
    public int DocumentTotalQuantity => LineItems.Sum(x => x.Quantity);
    public decimal DocumentGrossAmount => DocumentGrandTotal;
    public decimal DocumentDiscountAmount => LineItems.Sum(x => x.DiscountAmount);
    public decimal DocumentAdvanceWHTax => LineItems.Sum(x => x.AdvTaxAmount);
    public decimal DocumentTaxAmount => 0;
    public decimal DocumentCNValue => 0;
    public decimal DocumentAdjustmentsTotal =>
        0;
    public bool HasAdjustments => false;
    public decimal DocumentGrandTotal => LineItems.Sum(x => x.InvoiceItemTotal);
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
                ClinicName = GetSetting(dict, "PharmacyName", "ClinicName", "DR ASIF PHARMA");
                ClinicAddress = GetSetting(dict, "Address", "ClinicAddress", "Pirmahal, Near Imam Bargah");
                if (dict.TryGetValue("ClinicPhone",   out var p)) ClinicPhone   = p;
            });
        }
        catch { /* silently ignore — use defaults */ }
    }

    private static string GetSetting(IReadOnlyDictionary<string, string> settings, string primaryKey, string legacyKey, string fallback)
    {
        if (settings.TryGetValue(primaryKey, out var primary) && !string.IsNullOrWhiteSpace(primary)) return primary;
        if (settings.TryGetValue(legacyKey, out var legacy) && !string.IsNullOrWhiteSpace(legacy)) return legacy;
        return fallback;
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
                for (var i = 0; i < saleWithItems.Items.Count; i++)
                {
                    saleWithItems.Items[i].SerialNumber = i + 1;
                }

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
