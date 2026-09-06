using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.Messages;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Inventory;

public partial class InventoryViewModel : ViewModelBase, ISearchable, IRecipient<InventoryChangedMessage>
{
    private readonly ProductRepository _productRepo;
    private readonly ReturnRepository _returnRepo;

    public InventoryViewModel(ProductRepository productRepo, ReturnRepository returnRepo)
    {
        _productRepo = productRepo;
        _returnRepo = returnRepo;
        WeakReferenceMessenger.Default.RegisterAll(this);
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
    [ObservableProperty] private ObservableCollection<ProductStock> _availableStockBatches = new();
    [ObservableProperty] private ProductStock? _selectedStockBatch;
    [ObservableProperty] private int _adjustmentQuantity;
    [ObservableProperty] private string _adjustmentReason = string.Empty;
    [ObservableProperty] private DateTimeOffset _adjustmentDate=DateTimeOffset.Now;
    [ObservableProperty] private Product? _priceSelectedProduct;
    [ObservableProperty] private ObservableCollection<ProductStock> _priceAvailableStockBatches = new();
    [ObservableProperty] private ProductStock? _priceSelectedStockBatch;
    [ObservableProperty] private decimal _newMrpValue;
    public decimal CurrentPackMrp => PriceSelectedStockBatch?.MRP ?? 0;
    public decimal CurrentPieceMrp => CurrentPackMrp / Math.Max(1, PriceSelectedProduct?.PiecesPerUnit ?? 1);
    public decimal AdjustedPackMrpPreview => NewMrpValue;
    public decimal AdjustedPieceMrpPreview => AdjustedPackMrpPreview / Math.Max(1, PriceSelectedProduct?.PiecesPerUnit ?? 1);

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
            list = list.Where(m => (m.Name?.ToLower().Contains(term) ?? false));
        }

        AllStock = new ObservableCollection<Product>(list.OrderBy(m => m.Name));
        LowStock = new ObservableCollection<Product>(list.Where(m => m.IsLowStock && m.TotalStock > 0 && !m.IsExpired).OrderBy(m => m.TotalStock));
        OutOfStock = new ObservableCollection<Product>(list.Where(m => m.TotalStock <= 0).OrderBy(m => m.Name));
        Expired = new ObservableCollection<Product>(list.Where(m => m.IsExpired).OrderBy(m => m.EarliestExpiry));
        NearExpiry = new ObservableCollection<Product>(list.Where(m => m.EarliestExpiry.HasValue && !m.IsExpired && m.EarliestExpiry.Value.Date <= today.AddDays(30)).OrderBy(m => m.EarliestExpiry));
    }

    public async Task InitializeAsync()
    {
        try
        {
            var products = await Task.Run(() => _productRepo.GetAll());
            var metrics = await Task.Run(() => _productRepo.GetInventoryMetrics());
            var list = products.ToList();
            var today = DateTime.Today;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                _rawList = list;
                FilterInventory();

                TotalStockItems = metrics.TotalStockItems;
                LowStockCount = metrics.LowStockItems;
                OutOfStockCount = metrics.OutOfStockItems;
                ExpiredCount = metrics.ExpiredBatches;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load inventory: {ex.Message}");
        }
    }

    /// <summary>Refresh when stock changes elsewhere (sales, purchases, or returns).</summary>
    public void Receive(InventoryChangedMessage message) => _ = InitializeAsync();

    partial void OnSelectedProductChanged(Product? value) => _ = LoadStockBatchesAsync(value);

    partial void OnPriceSelectedProductChanged(Product? value) => _ = LoadPriceStockBatchesAsync(value);

    private async Task LoadPriceStockBatchesAsync(Product? product)
    {
        PriceSelectedStockBatch = null;
        PriceAvailableStockBatches.Clear();
        if (product == null) return;
        var batches = await Task.Run(() => _productRepo.GetActiveStockBatches(product.ProductID));
        foreach (var batch in batches)
            PriceAvailableStockBatches.Add(batch);
    }

    partial void OnPriceSelectedStockBatchChanged(ProductStock? value)
    {
        NewMrpValue = value?.MRP ?? 0;
        NotifyPricePreview();
    }

    partial void OnNewMrpValueChanged(decimal value) => NotifyPricePreview();

    private void NotifyPricePreview()
    {
        OnPropertyChanged(nameof(CurrentPackMrp));
        OnPropertyChanged(nameof(CurrentPieceMrp));
        OnPropertyChanged(nameof(AdjustedPackMrpPreview));
        OnPropertyChanged(nameof(AdjustedPieceMrpPreview));
    }

    private async Task LoadStockBatchesAsync(Product? product)
    {
        SelectedStockBatch = null;
        AvailableStockBatches.Clear();
        if (product == null) return;

        var batches = await Task.Run(() => _productRepo.GetActiveStockBatches(product.ProductID));
        foreach (var batch in batches)
            AvailableStockBatches.Add(batch);
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

        if (SelectedStockBatch == null)
        {
            StatusMessage = "Please select an expiry batch.";
            return;
        }

        // Convert pack input → piece delta
        int piecesPerUnit = SelectedProduct.PiecesPerUnit > 0 ? SelectedProduct.PiecesPerUnit : 1;
        int deltaPieces = AdjustmentQuantity * piecesPerUnit;

        try
        {
            var newQuantity = SelectedStockBatch.QuantityAvailable + deltaPieces;
            if (newQuantity < 0)
            {
                StatusMessage = $"Cannot adjust below zero for the selected batch ({SelectedStockBatch.QuantityAvailable} pieces available; expires {SelectedStockBatch.ExpiryDate:dd MMM yyyy}).";
                return;
            }

            await Task.Run(() => _productRepo.AdjustStock(SelectedStockBatch.StockID, newQuantity));
            StatusMessage = $"Stock adjusted for {SelectedProduct.Name}: {(AdjustmentQuantity > 0 ? "+" : "")}{AdjustmentQuantity} pack(s) = {(deltaPieces > 0 ? "+" : "")}{deltaPieces} pieces.";
            
            SelectedProduct = null;
            SelectedStockBatch = null;
            AvailableStockBatches.Clear();
            AdjustmentQuantity = 0;
            AdjustmentReason = string.Empty;
            AdjustmentDate=DateTimeOffset.Now;
            
            await InitializeAsync();
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to adjust stock: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AdjustPriceAsync()
    {
        if (PriceSelectedProduct == null || PriceSelectedStockBatch == null)
        {
            StatusMessage = "Please select a product and expiry batch.";
            return;
        }
        if (NewMrpValue <= 0)
        {
            StatusMessage = "Enter an MRP greater than zero.";
            return;
        }

        var packMrp = Math.Round(AdjustedPackMrpPreview, 2, MidpointRounding.AwayFromZero);
        var pieceMrp = Math.Round(packMrp / Math.Max(1, PriceSelectedProduct.PiecesPerUnit), 2, MidpointRounding.AwayFromZero);
        try
        {
            await Task.Run(() => _productRepo.AdjustBatchMrp(PriceSelectedStockBatch.StockID, PriceSelectedProduct.ProductID, packMrp));
            StatusMessage = $"MRP updated for {PriceSelectedProduct.Name} ({PriceSelectedStockBatch.ExpiryDate:MMM yyyy}): Pack Rs. {packMrp:N2} | Piece Rs. {pieceMrp:N2}.";
            LogActivity("Batch MRP Adjustment", $"Updated {PriceSelectedProduct.Name} batch #{PriceSelectedStockBatch.StockID} to Pack MRP Rs. {packMrp:N2} / Piece Rs. {pieceMrp:N2}", "Inventory");

            // A completed price adjustment starts a clean operation. Clear all
            // price-module selections explicitly before refreshing collections,
            // otherwise the ComboBox can retain the old batch object visually.
            PriceSelectedStockBatch = null;
            PriceAvailableStockBatches.Clear();
            PriceSelectedProduct = null;
            NewMrpValue = 0;
            NotifyPricePreview();

            await InitializeAsync();
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
        }
        catch (Exception ex) { StatusMessage = $"Failed to adjust MRP: {ex.Message}"; }
    }

    [RelayCommand]
    private void OpenSupplierReturnModal(Product p)
    {
        if (p == null) return;
        ReturnTargetProduct = p;
        SupplierReturnQuantity = p.TotalStock > 0 ? p.TotalStock : 0;
        SupplierCreditAmount = p.PricePerTablet * SupplierReturnQuantity;
        SupplierReturnNotes = "Expired Return";
        IsSupplierReturnModalOpen = true;
    }

    partial void OnSupplierReturnQuantityChanged(int value)
    {
        if (ReturnTargetProduct != null)
        {
            SupplierCreditAmount = value * ReturnTargetProduct.PricePerTablet;
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

        if (SupplierReturnQuantity > ReturnTargetProduct.TotalStock)
        {
            StatusMessage = $"Cannot return more than current stock ({ReturnTargetProduct.TotalStock}).";
            return;
        }

        var ret = new ProductReturn
        {
            ReturnNo = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
            ProductId = ReturnTargetProduct.ProductID,
            SupplierId = ReturnTargetProduct.SupplierID,
            BatchNo = ReturnTargetProduct.PCode.ToString(),
            Quantity = SupplierReturnQuantity,
            ReturnType = "Supplier Return",
            Reason = "Expired",
            Notes = SupplierReturnNotes,
            RefundAmount = SupplierCreditAmount, // Recorded as refund amount (credit in dashboard)
            CreatedBy = CurrentUser?.UserID,
            CreatedAt = DateTime.Now,
            IsPosted = true
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
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "Failed to process return: " + ex.Message);
        }
    }
}

