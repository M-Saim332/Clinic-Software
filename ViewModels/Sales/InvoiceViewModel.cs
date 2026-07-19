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
    private readonly SaleRepository _saleRepo;
    public Action? RequestGoBack { get; set; }
    public Action? RequestPrint { get; set; }

    public InvoiceViewModel(SaleRepository saleRepo)
    {
        _saleRepo = saleRepo;
    }

    [ObservableProperty] private Sale? _saleData;
    [ObservableProperty] private ObservableCollection<SaleItem> _lineItems = new();
    [ObservableProperty] private string _statusMessage = string.Empty;

    public void LoadInvoice(Sale sale)
    {
        SaleData = sale;
        _ = LoadItemsAsync(sale.SaleID);
    }

    private async Task LoadItemsAsync(int saleId)
    {
        var saleWithItems = await Task.Run(() => _saleRepo.GetByIdWithItems(saleId));
        if (saleWithItems != null && saleWithItems.Items != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LineItems = new ObservableCollection<SaleItem>(saleWithItems.Items);
            });
        }
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
}
