using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Core.Enums;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class SaleViewModel : ViewModelBase, ISearchable, INavigationContext, IRecipient<InventoryChangedMessage>
{
    public event Action? RequestAddProduct;
    public int? PreselectedEntityId { get; set; }
    public Action<int>? ReturnToCaller { get; set; }
    private readonly SaleRepository _repo;
    private readonly ProductRepository _productRepo;

    private readonly ActivityLogRepository _activityRepo;

    public InvoiceViewModel InvoiceVM { get; }

    public SaleViewModel(
        SaleRepository repo, 
        ProductRepository productRepo,
        ActivityLogRepository activityRepo,
        InvoiceViewModel invoiceVM)
    {
        _repo = repo;
        _productRepo = productRepo;
        _activityRepo = activityRepo;
        InvoiceVM = invoiceVM;
        WeakReferenceMessenger.Default.RegisterAll(this);

        InvoiceVM.RequestGoBack += () => ShowInvoicePrint = false;
    }

    /// <summary>Refreshes the product search list after batch/product archival.</summary>
    public void Receive(InventoryChangedMessage message) => _ = InitializeAsync();

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showForm;
    [ObservableProperty] private bool _showInvoicePrint;
    
    // Auto-update visibility flags
    partial void OnShowFormChanged(bool value) => OnPropertyChanged(nameof(ShowList));
    partial void OnShowInvoicePrintChanged(bool value) => OnPropertyChanged(nameof(ShowList));
    public bool ShowList => !ShowForm && !ShowInvoicePrint;
    
    [ObservableProperty] private ObservableCollection<Sale> _sales = new();
    private ObservableCollection<Sale> _allSales = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new(); // non-expired
    [ObservableProperty] private Sale? _selectedSale;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Invoices...";

    partial void OnSearchTermChanged(string value) => FilterSales();

    private void FilterSales()
    {
        IEnumerable<Sale> result;
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            result = _allSales;
        }
        else
        {
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            result = _allSales.Where(s =>
                (s.InvoiceNumber?.ToLower().Contains(term) ?? false) ||
                (s.PatientName?.ToLower().Contains(term) ?? false));
        }

        Sales.Clear();
        foreach (var item in result)
            Sales.Add(item);
    }

    // KPI summary counts
    [ObservableProperty] private int _totalInvoicesCount;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _revenueToday;
    [ObservableProperty] private decimal _avgSaleValue;

    // Header Fields
    [ObservableProperty] private string _invoiceNumber = string.Empty;
    [ObservableProperty] private string _patientName = string.Empty;
    [ObservableProperty] private DateTimeOffset _saleDate = DateTimeOffset.Now;
    [ObservableProperty] private decimal _salesTax;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraftInvoice))]
    [NotifyPropertyChangedFor(nameof(IsCheckingInvoice))]
    [NotifyPropertyChangedFor(nameof(IsPostedInvoice))]
    [NotifyPropertyChangedFor(nameof(IsNewSale))]
    private InvoiceState _invoiceState = InvoiceState.Draft;
    [ObservableProperty] private string _paymentMethod = "Cash";

    public List<string> PaymentMethods { get; } = new() { "Cash", "Card", "Online" };

    // Line Items
    [ObservableProperty] private ObservableCollection<SaleItem> _lineItems = new();

    // Line Item Input
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _productSearchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<Product> _filteredProducts = new();
    private CancellationTokenSource? _productSearchCancellation;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _productPrice;
    [ObservableProperty] private string _selectedUnitType = "Piece";
    [ObservableProperty] private bool _showDeleteAllConfirm;

    public List<string> UnitTypes { get; } = new() { "Piece" };
    public string LoggedInUserName => CurrentUser?.DisplayName ?? "Unknown";
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
    public string AvailableStockDisplay => SelectedProduct == null
        ? string.Empty
        : $"Available stock: {SelectedProduct.StockBreakdown}";
    
    public int MaxQuantity
    {
        get
        {
            if (SelectedProduct == null) return 1;
            var availablePieces = SelectedProduct.TotalStock - LineItems
                .Where(item => item.ProductID == SelectedProduct.ProductID)
                .Sum(item => item.StockQuantity);
            return Math.Max(0, availablePieces);
        }
    }

    private bool CanAddLineItem() => SelectedProduct != null && Quantity > 0 && Quantity <= MaxQuantity;

    // LineNetTotal includes the per-line tax after applying the unit price and discount.
    // Keeping the invoice summary on this value makes a Pack/Piece switch refresh the
    // payable amount immediately and keeps the persisted Sale.GrandTotal consistent.
    public decimal Subtotal => LineItems.Sum(x => x.LineNetTotal);
    public decimal GrandTotal => Subtotal;
    public bool IsDraftInvoice => InvoiceState == InvoiceState.Draft;
    public bool IsCheckingInvoice => InvoiceState == InvoiceState.Checking;
    public bool IsPostedInvoice => InvoiceState == InvoiceState.Posted;

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && Mode == FormMode.Add;
    public bool IsNewSale => Mode == FormMode.Add && InvoiceState == InvoiceState.Draft;

    [ObservableProperty] private bool _isLoadedFromHandoff;
    [ObservableProperty] private string _handoffSourceText = string.Empty;

    [RelayCommand]
    private async Task NewAsync()
    {
        ClearFields();

        // Ensure patients and products are loaded before showing the form
        try
        {
            var products = await Task.Run(() => _productRepo.GetAll());
            Products = new System.Collections.ObjectModel.ObservableCollection<Product>(
                products.Where(m => !m.IsExpired).OrderBy(m => m.Name));
        }
        catch { /* silently keep existing lists if refresh fails */ }

        InvoiceNumber = "Assigned after posting";
        InvoiceState = InvoiceState.Draft;
        Mode = FormMode.Add;
        ShowForm = true;
        NotifyButtonStates();
        StatusMessage = "Create new sale invoice.";
    }

    public async Task LoadFromHandoffAsync(Prescription handoff)
    {
        await NewAsync();
        
        IsLoadedFromHandoff = true;
        HandoffSourceText = $"[ 📋 Loaded from Doctor Handoff - Visit #{handoff.VisitDate:yyyyMMdd}-{handoff.PrescriptionID} ]";
        PatientName = handoff.PatientName ?? string.Empty;
        
        LineItems.Clear();
        foreach (var pItem in handoff.Items)
        {
            var match = Products.FirstOrDefault(p => 
                p.Name.Equals(pItem.ProductName, StringComparison.OrdinalIgnoreCase) ||
                (p.GenericName != null && p.GenericName.Equals(pItem.ProductName, StringComparison.OrdinalIgnoreCase)));
                
            if (match != null)
            {
                var price = match.SellingPrice > 0 && match.PiecesPerUnit > 0
                    ? match.SellingPrice / match.PiecesPerUnit
                    : (match.Rate > 0 && match.PiecesPerUnit > 0 
                        ? match.Rate / match.PiecesPerUnit 
                        : match.PurchasePrice);
                var qty = Math.Min(pItem.Quantity, match.TotalStock);
                
                if (qty > 0)
                {
                    var item = new SaleItem
                    {
                        ProductID = match.ProductID,
                        ProductName = match.Name,
                        Quantity = qty,
                        UnitTypeSold = "Piece",
                        StockQuantity = qty,
                        Discount = 0,
                        Tax = 0,
                        ProductPrice = price
                    };
                    ConfigureBatch(item, match);
                    if (item.SelectedStockBatch == null)
                        continue;
                    item.LineTotal = item.LineNetTotal;
                    LineItems.Add(item);
                }
            }
        }
        
        OnPropertyChanged(nameof(GrandTotal));
        StatusMessage = "Loaded prescribed items from doctor handoff.";
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedSale == null) { StatusMessage = "Select a sale first."; return; }

        InvoiceVM.LoadInvoice(SelectedSale);
        ShowForm = false;
        ShowInvoicePrint = true;
        StatusMessage = $"Opening invoice {SelectedSale.InvoiceNumber}";
        LogActivity("Invoice Viewed", $"Invoice #{SelectedSale.InvoiceNumber} opened for document view", "Sales");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void PrintInvoice()
    {
        if (SelectedSale == null) { StatusMessage = "Select a sale first."; return; }
        InvoiceVM.LoadInvoice(SelectedSale);
        ShowInvoicePrint = true;
        StatusMessage = $"Opening invoice {SelectedSale.InvoiceNumber}";
        LogActivity("Invoice Generated", $"Invoice #{SelectedSale.InvoiceNumber} generated for printing", "Sales");
    }

    [RelayCommand(CanExecute = nameof(CanAddLineItem))]
    private void AddLineItem()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }
        var stockQuantity = Quantity;
        var alreadyAllocated = LineItems.Where(x => x.ProductID == SelectedProduct.ProductID).Sum(x => x.StockQuantity);
        if (stockQuantity + alreadyAllocated > SelectedProduct.TotalStock) { StatusMessage = $"Only {SelectedProduct.TotalStock - alreadyAllocated} tablet/unit(s) remain available for this invoice."; return; }

        // Compute line total using percentage-based discount and tax
        var item = new SaleItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            Quantity = Quantity,
            UnitTypeSold = "Piece",
            StockQuantity = stockQuantity,
            Discount = Discount,
            Tax = Tax,
            ProductPrice = ProductPrice
        };
        ConfigureBatch(item, SelectedProduct);
        if (item.SelectedStockBatch == null)
        {
            StatusMessage = "No active batch is available for the selected product.";
            return;
        }
        var alreadyReserved = LineItems
            .Where(line => line.StockID == item.StockID)
            .Sum(line => line.StockQuantity);
        if (alreadyReserved + item.StockQuantity > item.SelectedStockBatch.QuantityAvailable)
        {
            StatusMessage = $"Only {item.SelectedStockBatch.QuantityAvailable - alreadyReserved} piece(s) remain in the selected batch.";
            return;
        }
        item.LineTotal = item.LineNetTotal;

        LineItems.Add(item);
        RecalculateInvoiceTotals();
        
        // Reset inputs
        SelectedProduct = null;
        Quantity = 1;
        Discount = 0;
        Tax = 0;
        ProductPrice = 0;
        SelectedUnitType = "Piece";
        StatusMessage = string.Empty;
        
        OnPropertyChanged(nameof(MaxQuantity));
        AddLineItemCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveLineItem(SaleItem item)
    {
            if (item != null && LineItems.Contains(item))
        {
            LineItems.Remove(item);
            RecalculateInvoiceTotals();
        }
    }

    [RelayCommand]
    private async Task PostSaleAsync()
    {
        if (InvoiceState == InvoiceState.Draft) { CheckInvoice(); return; }
        if (LineItems.Count == 0) { StatusMessage = "Add at least one product."; return; }
        string patientNameForSale = string.IsNullOrWhiteSpace(PatientName) ? "Anonymous" : PatientName.Trim();

        var s = new Sale
        {
            InvoiceNumber = InvoiceNumber,
            SaleDate = SaleDate.DateTime,
            PatientID = null,
            PatientName = patientNameForSale,
            SalesTax = SalesTax,
            GrandTotal = GrandTotal,
            PaymentMethod = PaymentMethod,
            IsPosted = true,
            ReceptionistId = CurrentUser?.UserID,
            ReceptionistName = LoggedInUserName,
            Items = LineItems.ToList()
        };

        try
        {
            int savedSaleId = 0;
            if (Mode == FormMode.Add)
            {
                savedSaleId = await Task.Run(() => _repo.Insert(s));
                InvoiceState = InvoiceState.Posted;
                StatusMessage = "Sale posted successfully. Stock updated.";
                LogActivity("Sale Completed", $"Invoice #{s.InvoiceNumber} posted for {s.PatientName} — Rs. {s.GrandTotal:N2}", "Sales");
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            }
            
            await InitializeAsync();
            
            if (savedSaleId > 0)
            {
                var postedSale = await Task.Run(() => _repo.GetByIdWithItems(savedSaleId));
                if (postedSale != null)
                {
                    InvoiceVM.LoadInvoice(postedSale);
                    ResetFormToList();
                    ShowInvoicePrint = true;
                }
            }
            else
            {
                await NewAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving sale: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RequestDeleteAll()
    {
        if (!IsAdmin) { StatusMessage = "Only an administrator can archive all sales."; return; }
        ShowDeleteAllConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAllAsync()
    {
        ShowDeleteAllConfirm = false;
        if (!IsAdmin) { StatusMessage = "Administrator authorization is required."; return; }
        var count = await Task.Run(_repo.SoftDeleteAll);
        StatusMessage = $"{count} sale(s) archived. Financial history remains in the database.";
        LogActivity("Sales Archived", $"Archived all {count} active sales", "Sales");
        await InitializeAsync();
    }

    [RelayCommand] private void CancelDeleteAll() => ShowDeleteAllConfirm = false;

    [RelayCommand]
    private void Cancel()
    {
        ResetFormToList();
        StatusMessage = string.Empty;
    }

    /// <summary>Defaults a sales line to the earliest-expiring active batch (FEFO).</summary>
    private void ConfigureBatch(SaleItem item, Product product)
    {
        // Query the live batch table at line creation so the POS default always
        // reflects current FEFO availability rather than a stale product total.
        var batches = _productRepo.GetActiveStockBatches(product.ProductID).ToList();

        item.PiecesPerUnit = Math.Max(1, product.PiecesPerUnit);
        item.AvailableStockBatches = batches;
        item.SelectedStockBatch = batches.FirstOrDefault();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SaleItem.SelectedStockBatch) or nameof(SaleItem.ProductPrice) or nameof(SaleItem.UnitTypeSold) or nameof(SaleItem.Quantity))
            {
                item.LineTotal = item.LineNetTotal;
                RecalculateInvoiceTotals();
            }
        };
    }

    /// <summary>Discard the current in-memory invoice without writing or posting it.</summary>
    private void ResetFormToList()
    {
        ClearFields();
        Mode = FormMode.View;
        InvoiceState = InvoiceState.Draft;
        ShowForm = false;
        ShowInvoicePrint = false;
        NotifyButtonStates();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var products = await Task.Run(() => _productRepo.GetAll());
            var sales = await Task.Run(() => _repo.GetAll());
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                Products = new ObservableCollection<Product>(
                    products.Where(m => !m.IsExpired).OrderBy(m => m.Name));
                FilteredProducts = new ObservableCollection<Product>(Products);
                var sorted = new ObservableCollection<Sale>(sales.OrderByDescending(s => s.SaleDate));
                _allSales = sorted;
                FilterSales();

                TotalInvoicesCount = Sales.Count;
                TotalRevenue = Sales.Sum(s => s.GrandTotal);
                RevenueToday = Sales.Where(s => s.SaleDate.Date == DateTime.Today).Sum(s => s.GrandTotal);
                AvgSaleValue = TotalInvoicesCount > 0 ? TotalRevenue / TotalInvoicesCount : 0;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load data: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        InvoiceNumber = string.Empty;
        PatientName = string.Empty;
        SaleDate = DateTimeOffset.Now;
        SalesTax = 0;
        PaymentMethod = "Cash";
        LineItems.Clear();
        SelectedProduct = null;
        Quantity = 1;
        Discount = 0;
        Tax = 0;
        ProductPrice = 0;
        ProductSearchTerm = string.Empty;
        SelectedUnitType = "Piece";
        IsLoadedFromHandoff = false;
        HandoffSourceText = string.Empty;
        RecalculateInvoiceTotals();
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsNewSale));
    }

    partial void OnProductSearchTermChanged(string value)
    {
        _productSearchCancellation?.Cancel();
        _productSearchCancellation?.Dispose();
        _productSearchCancellation = new CancellationTokenSource();
        _ = FilterProductsAfterTypingPauseAsync(value, _productSearchCancellation.Token);
    }

    /// <summary>Debounces only the POS suggestion list; pricing, FIFO, and cart logic are untouched.</summary>
    private async Task FilterProductsAfterTypingPauseAsync(string? searchText, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            var term = searchText?.Trim() ?? string.Empty;
            var matches = (string.IsNullOrEmpty(term) ? Products : Products.Where(m =>
                    m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (m.GenericName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (m.CompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)))
                .Take(30)
                .ToList();

            if (!cancellationToken.IsCancellationRequested)
                FilteredProducts = new ObservableCollection<Product>(matches);
        }
        catch (OperationCanceledException) { }
    }

    /*
     * Product lookup remains an in-memory filter over the already-loaded active catalog.
     * The 30-result cap applies only to the POS suggestion popup, not reports or catalog search.
     */
    partial void OnSelectedProductChanged(Product? value)
    {
        _ = RefreshSelectedProductPricingAsync(value);
        OnPropertyChanged(nameof(AvailableStockDisplay));
        OnPropertyChanged(nameof(MaxQuantity));
        AddLineItemCommand.NotifyCanExecuteChanged();
    }

    partial void OnQuantityChanged(int value) => RecalculateInvoiceTotals();

    partial void OnDiscountChanged(decimal value) => RecalculateInvoiceTotals();
    partial void OnProductPriceChanged(decimal value) => RecalculateInvoiceTotals();

    partial void OnSelectedUnitTypeChanged(string value)
    {
        if (SelectedProduct != null) ProductPrice = GetSelectedUnitPrice(SelectedProduct);
        OnPropertyChanged(nameof(AvailableStockDisplay));
        OnPropertyChanged(nameof(MaxQuantity));
    }

    private decimal GetSelectedUnitPrice(Product product)
    {
        var piecesPerPack = Math.Max(1, product.PiecesPerUnit);
        if (product.SellingPrice > 0)
            return product.SellingPrice / piecesPerPack;
        if (product.Rate > 0)
            return product.Rate / piecesPerPack;
        return product.PurchasePrice;
    }

    /// <summary>Uses the live FEFO batch price for the entry panel, not a cached product rate.</summary>
    private async Task RefreshSelectedProductPricingAsync(Product? product)
    {
        if (product == null)
        {
            ProductPrice = 0;
            return;
        }

        var batches = await Task.Run(() => _productRepo.GetActiveStockBatches(product.ProductID).ToList());
        if (SelectedProduct?.ProductID != product.ProductID) return;

        var fifoBatch = batches.FirstOrDefault();
        if (fifoBatch == null)
        {
            ProductPrice = 0;
            StatusMessage = "Product Out of Stock.";
            return;
        }

        ProductPrice = fifoBatch.MRP / Math.Max(1, product.PiecesPerUnit);
        StatusMessage = string.Empty;
    }

    /// <summary>Single recalculation path for add, quantity edit, batch change, and delete.</summary>
    private void RecalculateInvoiceTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(MaxQuantity));
        AddLineItemCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] private void QuickAddProduct() => RequestAddProduct?.Invoke();

    public async Task PreselectProductAsync(int id)
    {
        Products = new ObservableCollection<Product>((await Task.Run(_productRepo.GetAll)).Where(p => !p.IsExpired).OrderBy(p => p.Name));
        FilteredProducts = new ObservableCollection<Product>(Products);
        SelectedProduct = Products.FirstOrDefault(p => p.ProductID == id);
        PreselectedEntityId = id;
    }
    
    partial void OnSalesTaxChanged(decimal value) => RecalculateInvoiceTotals();

    [RelayCommand]
    private void CheckInvoice()
    {
        if (LineItems.Count == 0) { StatusMessage = "Add at least one product before checking the invoice."; return; }
        InvoiceState = InvoiceState.Checking;
        StatusMessage = "Invoice checked. Review the totals, then post it.";
    }

    [RelayCommand]
    private void EditInvoice() { InvoiceState = InvoiceState.Draft; StatusMessage = "Invoice returned to draft."; }
}

