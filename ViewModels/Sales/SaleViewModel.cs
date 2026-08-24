using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Core.Enums;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class SaleViewModel : ViewModelBase, ISearchable, INavigationContext
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

        InvoiceVM.RequestGoBack += () => ShowInvoicePrint = false;
    }

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
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Sales = new ObservableCollection<Sale>(_allSales);
        }
        else
        {
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            Sales = new ObservableCollection<Sale>(
                _allSales.Where(s => 
                    s.InvoiceNumber.ToLower().Contains(term) ||
                    (s.PatientName?.ToLower().Contains(term) ?? false)));
        }
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
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _productPrice;
    [ObservableProperty] private string _selectedUnitType = "Tablet";
    [ObservableProperty] private bool _showDeleteAllConfirm;

    public List<string> UnitTypes { get; } = new() { "Pieces" };
    public string LoggedInUserName => CurrentUser?.DisplayName ?? "Unknown";
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
    public string AvailableStockDisplay => SelectedProduct == null
        ? string.Empty
        : $"Available stock: {SelectedProduct.Stock} pieces";

    public decimal Subtotal => LineItems.Sum(x => x.LineNetTotal);
    public decimal GrandTotal => Subtotal + (Subtotal * SalesTax / 100m);
    public bool IsDraftInvoice => InvoiceState == InvoiceState.Draft;
    public bool IsCheckingInvoice => InvoiceState == InvoiceState.Checking;
    public bool IsPostedInvoice => InvoiceState == InvoiceState.Posted;

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && Mode == FormMode.Add;
    public bool IsNewSale => Mode == FormMode.Add && InvoiceState == InvoiceState.Draft;

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

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedSale == null) { StatusMessage = "Select a sale first."; return; }
        
        try
        {
            InvoiceNumber = SelectedSale.InvoiceNumber;
            SaleDate = new DateTimeOffset(SelectedSale.SaleDate);
            PaymentMethod = SelectedSale.PaymentMethod ?? "Cash";
            
            var saleWithItems = await Task.Run(() => _repo.GetByIdWithItems(SelectedSale.SaleID));
            var items = saleWithItems?.Items ?? new List<SaleItem>();
            PatientName = saleWithItems?.PatientName ?? SelectedSale.PatientName ?? string.Empty;
            LineItems = new ObservableCollection<SaleItem>(items);
            SalesTax = saleWithItems?.SalesTax ?? 0;
            InvoiceState = saleWithItems?.IsPosted == true ? InvoiceState.Posted : InvoiceState.Draft;
            
            OnPropertyChanged(nameof(GrandTotal));
            
            Mode = FormMode.View; 
            ShowForm = true;
            NotifyButtonStates();
            StatusMessage = $"Viewing details for {InvoiceNumber}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sale details: {ex.Message}";
        }
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

    [RelayCommand]
    private void AddLineItem()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }
        var stockQuantity = Quantity;
        var alreadyAllocated = LineItems.Where(x => x.ProductID == SelectedProduct.ProductID).Sum(x => x.StockQuantity);
        if (stockQuantity + alreadyAllocated > SelectedProduct.Stock) { StatusMessage = $"Only {SelectedProduct.Stock - alreadyAllocated} tablet/unit(s) remain available for this invoice."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateDiscountPercentage(Discount)) { StatusMessage = "Discount must be between 0% and 100%."; return; }

        // Compute line total using percentage-based discount and tax
        var item = new SaleItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            Quantity = Quantity,
            UnitTypeSold = "Pieces",
            StockQuantity = stockQuantity,
            Discount = Discount,
            Tax = Tax,
            ProductPrice = ProductPrice
        };
        // LineNetTotal is a computed property — store it as LineTotal for the DB
        item.LineTotal = item.LineNetTotal;

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        
        // Reset inputs
        SelectedProduct = null;
        Quantity = 1;
        Discount = 0;
        Tax = 0;
        ProductPrice = 0;
        SelectedUnitType = "Pieces";
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RemoveLineItem(SaleItem item)
    {
        if (item != null && LineItems.Contains(item))
        {
            LineItems.Remove(item);
            OnPropertyChanged(nameof(GrandTotal));
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
            if (Mode == FormMode.Add)
            {
                await Task.Run(() => _repo.Insert(s));
                InvoiceState = InvoiceState.Posted;
                StatusMessage = "Sale posted successfully. Stock updated.";
                LogActivity("Sale Completed", $"Invoice #{s.InvoiceNumber} posted for {s.PatientName} — Rs. {s.GrandTotal:N2}", "Sales");
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            }
            
            await InitializeAsync();
            await NewAsync();
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
        Mode = FormMode.View;
        ShowForm = false;
        NotifyButtonStates();
        StatusMessage = string.Empty;
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
        ProductSearchTerm = string.Empty;
        SelectedUnitType = "Pieces";
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsNewSale));
    }

    partial void OnProductSearchTermChanged(string value)
    {
        var term = value?.Trim().ToLower() ?? string.Empty;
        FilteredProducts = string.IsNullOrEmpty(term)
            ? new ObservableCollection<Product>(Products)
            : new ObservableCollection<Product>(
                Products.Where(m =>
                    m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (m.GenericName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (m.CompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
        {
            // Use the per-unit price (PackMRP / PiecesPerUnit) for POS dispensing
            ProductPrice = value.PricePerTablet > 0 ? value.PricePerTablet : value.PurchasePrice;
        }
        OnPropertyChanged(nameof(AvailableStockDisplay));
    }

    partial void OnSelectedUnitTypeChanged(string value)
    {
        if (SelectedProduct != null) ProductPrice = SelectedProduct.PricePerTablet > 0 ? SelectedProduct.PricePerTablet : SelectedProduct.PurchasePrice;
        OnPropertyChanged(nameof(AvailableStockDisplay));
    }

    [RelayCommand] private void QuickAddProduct() => RequestAddProduct?.Invoke();

    public async Task PreselectProductAsync(int id)
    {
        Products = new ObservableCollection<Product>((await Task.Run(_productRepo.GetAll)).Where(p => !p.IsExpired).OrderBy(p => p.Name));
        FilteredProducts = new ObservableCollection<Product>(Products);
        SelectedProduct = Products.FirstOrDefault(p => p.ProductID == id);
        PreselectedEntityId = id;
    }
    
    partial void OnSalesTaxChanged(decimal value) => OnPropertyChanged(nameof(GrandTotal));

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
