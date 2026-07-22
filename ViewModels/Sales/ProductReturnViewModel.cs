using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class ProductReturnViewModel : ViewModelBase
{
    private readonly ReturnRepository _repo;
    private readonly ProductRepository _productRepo;

    public ProductReturnViewModel(ReturnRepository repo, ProductRepository productRepo)
    {
        _repo = repo;
        _productRepo = productRepo;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    private FormMode _mode = FormMode.View;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<ProductReturn> _returns = new();
    
    // Dropdown sources
    [ObservableProperty] private ObservableCollection<Product> _products = new();
    public List<string> ReturnTypes { get; } = new() { "Patient Return", "Supplier Return" };
    public List<string> Reasons { get; } = new() { "Expired", "Damaged", "Wrong Item", "Patient Changed Mind", "Other" };

    // Form Fields
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _batchNo = string.Empty;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InventoryEffectHint))]
    private string _returnType = "Patient Return";
    [ObservableProperty] private string _reason = "Damaged";
    [ObservableProperty] private string _notes = string.Empty;

    public string InventoryEffectHint => ReturnType == "Patient Return"
        ? "Inventory Effect: Patient Return — Stock will increase (+Qty)"
        : "Inventory Effect: Supplier Return — Stock will decrease (-Qty)";

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    public async Task InitializeAsync()
    {
        try
        {
            var returnsList = await Task.Run(() => _repo.GetAll());
            var productsList = await Task.Run(() => _productRepo.GetAll());
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                Returns = new ObservableCollection<ProductReturn>(returnsList);
                Products = new ObservableCollection<Product>(productsList);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load returns: {ex.Message}");
        }
    }

    [RelayCommand]
    private void New()
    {
        SelectedProduct = null;
        BatchNo = string.Empty;
        Quantity = 1;
        ReturnType = "Patient Return";
        Reason = "Damaged";
        Notes = string.Empty;
        
        Mode = FormMode.Add;
        StatusMessage = "Enter return details.";
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Please select a product."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be greater than zero."; return; }

        try
        {
            var ret = new ProductReturn
            {
                ReturnNo = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
                ProductId = SelectedProduct.ProductID,
                BatchNo = BatchNo,
                Quantity = Quantity,
                ReturnType = ReturnType,
                Reason = Reason,
                Notes = Notes,
                CreatedBy = CurrentUser?.UserID,
                CreatedAt = DateTime.Now
            };

            await Task.Run(() => _repo.Insert(ret));

            StatusMessage = "Return processed successfully and inventory updated.";
            LogActivity("Return Created", $"Processed {ret.ReturnType} for {SelectedProduct.Name} (Qty: {ret.Quantity})", "Returns");
            
            Mode = FormMode.View;
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error processing return: {ex.Message}";
        }
    }
}
