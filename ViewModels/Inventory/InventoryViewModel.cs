using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Inventory;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly ProductRepository _productRepo;

    public InventoryViewModel(ProductRepository productRepo)
    {
        _productRepo = productRepo;
    }

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private ObservableCollection<Product> _allStock = new();
    [ObservableProperty] private ObservableCollection<Product> _lowStock = new();
    [ObservableProperty] private ObservableCollection<Product> _outOfStock = new();
    [ObservableProperty] private ObservableCollection<Product> _expired = new();
    [ObservableProperty] private ObservableCollection<Product> _nearExpiry = new();

    // KPI Summary counts
    [ObservableProperty] private int _totalStockItems;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _outOfStockCount;
    [ObservableProperty] private int _expiredCount;

    // Adjustment fields
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private int _adjustmentQuantity;
    [ObservableProperty] private string _adjustmentReason = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            var products = await Task.Run(() => _productRepo.GetAll());
            var list = products.ToList();
            var today = DateTime.Today;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                AllStock = new ObservableCollection<Product>(list.OrderBy(m => m.Name));
                
                LowStock = new ObservableCollection<Product>(
                    list.Where(m => m.IsLowStock && m.Stock > 0 && !m.IsExpired).OrderBy(m => m.Stock));
                    
                OutOfStock = new ObservableCollection<Product>(
                    list.Where(m => m.Stock <= 0).OrderBy(m => m.Name));
                    
                Expired = new ObservableCollection<Product>(
                    list.Where(m => m.IsExpired).OrderBy(m => m.ExpiryDate));
                    
                NearExpiry = new ObservableCollection<Product>(
                    list.Where(m => m.ExpiryDate.HasValue && !m.IsExpired && m.ExpiryDate.Value <= today.AddDays(30))
                        .OrderBy(m => m.ExpiryDate));

                TotalStockItems = AllStock.Count;
                LowStockCount = LowStock.Count;
                OutOfStockCount = OutOfStock.Count;
                ExpiredCount = Expired.Count;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load inventory: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AdjustStockAsync()
    {
        if (SelectedProduct == null)
        {
            StatusMessage = "Please select a product.";
            return;
        }

        if (AdjustmentQuantity == 0)
        {
            StatusMessage = "Quantity cannot be zero.";
            return;
        }

        if (SelectedProduct.Stock + AdjustmentQuantity < 0)
        {
            StatusMessage = "Cannot adjust below zero stock.";
            return;
        }

        try
        {
            await Task.Run(() => _productRepo.AddStock(SelectedProduct.ProductID, AdjustmentQuantity));
            StatusMessage = $"Stock adjusted for {SelectedProduct.Name} by {AdjustmentQuantity}.";
            
            SelectedProduct = null;
            AdjustmentQuantity = 0;
            AdjustmentReason = string.Empty;
            
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to adjust stock: {ex.Message}";
        }
    }
}
