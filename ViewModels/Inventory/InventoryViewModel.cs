using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Inventory;

public partial class InventoryViewModel : ViewModelBase, ISearchable
{
    private readonly ProductRepository _productRepo;
    private readonly ReturnRepository _returnRepo;

    public InventoryViewModel(ProductRepository productRepo, ReturnRepository returnRepo)
    {
        _productRepo = productRepo;
        _returnRepo = returnRepo;
    }

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private ObservableCollection<Product> _allStock = new();
    private List<Product> _rawList = new();
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

    // Supplier Return Fields
    [ObservableProperty] private bool _isSupplierReturnModalOpen;
    [ObservableProperty] private Product? _returnTargetProduct;
    [ObservableProperty] private int _supplierReturnQuantity;
    [ObservableProperty] private decimal _supplierCreditAmount;
    [ObservableProperty] private string _supplierReturnNotes = string.Empty;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Inventory...";

    partial void OnSearchTermChanged(string value) => FilterInventory();

    private void FilterInventory()
    {
        var today = DateTime.Today;
        var list = _rawList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.ToLower().Replace(" ", "");
            list = list.Where(m => m.Name.ToLower().Contains(term) || (m.BatchNumber?.ToLower().Contains(term) ?? false));
        }

        AllStock = new ObservableCollection<Product>(list.OrderBy(m => m.Name));
        LowStock = new ObservableCollection<Product>(list.Where(m => m.IsLowStock && m.Stock > 0 && !m.IsExpired).OrderBy(m => m.Stock));
        OutOfStock = new ObservableCollection<Product>(list.Where(m => m.Stock <= 0).OrderBy(m => m.Name));
        Expired = new ObservableCollection<Product>(list.Where(m => m.IsExpired).OrderBy(m => m.ExpiryDate));
        NearExpiry = new ObservableCollection<Product>(list.Where(m => m.ExpiryDate.HasValue && !m.IsExpired && m.ExpiryDate.Value <= today.AddDays(30)).OrderBy(m => m.ExpiryDate));
    }

    public async Task InitializeAsync()
    {
        try
        {
            var products = await Task.Run(() => _productRepo.GetAll());
            var list = products.ToList();
            var today = DateTime.Today;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                _rawList = list;
                FilterInventory();

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

    [RelayCommand]
    private void OpenSupplierReturnModal(Product p)
    {
        if (p == null) return;
        ReturnTargetProduct = p;
        SupplierReturnQuantity = p.Stock > 0 ? p.Stock : 0;
        SupplierCreditAmount = p.PurchasePrice * SupplierReturnQuantity;
        SupplierReturnNotes = "Expired Return";
        IsSupplierReturnModalOpen = true;
    }

    partial void OnSupplierReturnQuantityChanged(int value)
    {
        if (ReturnTargetProduct != null)
        {
            SupplierCreditAmount = value * ReturnTargetProduct.PurchasePrice;
        }
    }

    [RelayCommand]
    private void CloseSupplierReturnModal() => IsSupplierReturnModalOpen = false;

    [RelayCommand]
    private async Task SubmitSupplierReturnAsync()
    {
        if (ReturnTargetProduct == null) return;

        if (SupplierReturnQuantity <= 0)
        {
            StatusMessage = "Return quantity must be > 0.";
            return;
        }

        if (SupplierReturnQuantity > ReturnTargetProduct.Stock)
        {
            StatusMessage = $"Cannot return more than current stock ({ReturnTargetProduct.Stock}).";
            return;
        }

        var ret = new ProductReturn
        {
            ReturnNo = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
            ProductId = ReturnTargetProduct.ProductID,
            SupplierId = ReturnTargetProduct.SupplierID,
            BatchNo = ReturnTargetProduct.BatchNumber ?? string.Empty,
            Quantity = SupplierReturnQuantity,
            ReturnType = "Supplier Return",
            Reason = "Expired",
            Notes = SupplierReturnNotes,
            RefundAmount = SupplierCreditAmount, // Recorded as refund amount (credit in dashboard)
            CreatedBy = CurrentUser?.UserID,
            CreatedAt = DateTime.Now
        };

        try
        {
            await Task.Run(() => _returnRepo.Insert(ret));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = $"Successfully returned {SupplierReturnQuantity} item(s) to Supplier. Credit: Rs. {SupplierCreditAmount:N2}";
                LogActivity("Supplier Return", $"Returned {SupplierReturnQuantity} expired units of {ReturnTargetProduct.Name}", "Inventory");
                IsSupplierReturnModalOpen = false;
            });
            _ = InitializeAsync();
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "Failed to process return: " + ex.Message);
        }
    }
}
