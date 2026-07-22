using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Products;

public enum FormMode { View, Add, Edit }

public partial class ProductRegistryViewModel : ViewModelBase
{
    private readonly ProductRepository _repo;
    private readonly CompanyRepository _companyRepo;
    private readonly ReturnRepository _returnRepo;

    public ProductRegistryViewModel(ProductRepository repo, CompanyRepository companyRepo, ReturnRepository returnRepo)
    {
        _repo = repo;
        _companyRepo = companyRepo;
        _returnRepo = returnRepo;
    }

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    // ── KPI Summary Card properties ────────────────────────────────────
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private string _totalInventoryValue = "Rs. 0.00";

    public string MutationEnabled_str => Mode.ToString();
    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    // ── Tab selection ──────────────────────────────────────────────────
    [ObservableProperty] private int _selectedTab = 0; // 0=All, 1=Expired, 2=Unsold
    partial void OnSelectedTabChanged(int value) => FilterProducts();

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Product> _filteredProducts = new();
    [ObservableProperty] private Product? _selectedProduct;

    // Companies for selection ComboBox
    [ObservableProperty] private ObservableCollection<Company> _companies = new();
    [ObservableProperty] private Company? _selectedCompany;

    // Fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _genericName = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _rack = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _batchNumber = string.Empty;
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private string _purchasePrice = "0.00";
    [ObservableProperty] private string _sellingPrice = "0.00";
    [ObservableProperty] private string _stock = "0";
    [ObservableProperty] private string _minimumStockLevel = "10";

    public string ProfitPerUnitText => FormatMoney(CurrentSellingPrice - CurrentPurchasePrice);
    public string InventoryValueText => FormatMoney(CurrentStock * CurrentSellingPrice);

    private int CurrentStock => int.TryParse(Stock, out var value) ? value : 0;
    private decimal CurrentPurchasePrice => decimal.TryParse(PurchasePrice, out var value) ? value : 0;
    private decimal CurrentSellingPrice => decimal.TryParse(SellingPrice, out var value) ? value : 0;

    // ── Delete confirmation state ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    private Product? _pendingDeleteProduct;
    [ObservableProperty] private bool _showDeleteConfirm;
    public string PendingDeleteLabel => PendingDeleteProduct is { } p ? p.Name : string.Empty;

    // ── Return Modal State (Patient Return & Supplier Return) ──────────
    [ObservableProperty] private bool _isReturnModalOpen;
    [ObservableProperty] private string _returnModalTitle = string.Empty;
    [ObservableProperty] private string _returnType = "Patient Return"; // "Patient Return" or "Supplier Return"
    [ObservableProperty] private int _returnQuantity = 1;
    [ObservableProperty] private string _returnReason = "Patient Changed Mind";
    [ObservableProperty] private Product? _returnTargetProduct;
    public string ReturnModalSubtitle => ReturnTargetProduct != null
        ? $"Product: {ReturnTargetProduct.Name} | Stock: {ReturnTargetProduct.Stock}"
        : string.Empty;

    public List<string> PatientReturnReasons { get; } = new() { "Patient Changed Mind", "Wrong Item", "Damaged", "Expired", "Other" };
    public List<string> SupplierReturnReasons { get; } = new() { "Expired", "Damaged", "Unsold / Slow Moving", "Wrong Item", "Other" };
    public List<string> ReturnReasons => ReturnType == "Patient Return" ? PatientReturnReasons : SupplierReturnReasons;

    [RelayCommand]
    private async Task NewAsync()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new product details.";
        var comps = await Task.Run(() => _companyRepo.GetAll());
        Companies = new ObservableCollection<Company>(comps);
    }

    // ── Row-level commands (match Patients pattern) ────────────────────
    [RelayCommand]
    private async Task EditSpecificAsync(Product product)
    {
        if (product == null) return;
        SelectedProduct = product;
        var comps = await Task.Run(() => _companyRepo.GetAll());
        Companies = new ObservableCollection<Company>(comps);
        FillFields(product);
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == product.CompanyID);
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Edit product details and click Save.";
    }

    [RelayCommand]
    private void RequestDeleteSpecific(Product product)
    {
        if (product == null) return;
        PendingDeleteProduct = product;
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var target = PendingDeleteProduct;
        ShowDeleteConfirm = false;
        PendingDeleteProduct = null;
        if (target == null) return;

        var ok = await Task.Run(() => _repo.Delete(target.ProductID));
        if (ok)
        {
            StatusMessage = "Product deleted.";
            LogActivity("Product Deleted", $"Deleted product '{target.Name}'", "Products");
            if (SelectedProduct?.ProductID == target.ProductID) SelectedProduct = null;
            _ = InitializeAsync();
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
        }
        else
        {
            StatusMessage = "Cannot delete — product is referenced in sales or purchases.";
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
        PendingDeleteProduct = null;
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product first."; return; }
        await EditSpecificAsync(SelectedProduct);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product first."; return; }
        RequestDeleteSpecificCommand.Execute(SelectedProduct);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name is required."; return; }
        if (!decimal.TryParse(PurchasePrice, out var purchase) || purchase < 0) { StatusMessage = "Enter a valid purchase price."; return; }
        if (!decimal.TryParse(SellingPrice, out var sell) || sell < 0) { StatusMessage = "Enter a valid selling price."; return; }
        if (!int.TryParse(Stock, out var stock) || stock < 0) { StatusMessage = "Enter valid stock."; return; }
        if (!int.TryParse(MinimumStockLevel, out var minStock) || minStock < 0) { StatusMessage = "Enter valid minimum stock."; return; }

        var m = BuildProduct();
        try
        {
            await Task.Run(() =>
            {
                if (Mode == FormMode.Add)
                {
                    _repo.Insert(m);
                }
                else
                {
                    m.ProductID = SelectedProduct!.ProductID;
                    _repo.Update(m);
                }
            });

            StatusMessage = Mode == FormMode.Add ? "Product added." : "Product updated.";
            if (Mode == FormMode.Add)
                LogActivity("Product Added", $"New product '{m.Name}' added to inventory", "Products");
            else
                LogActivity("Product Updated", $"Product '{m.Name}' was updated", "Products");
            
            Mode = FormMode.View;
            NotifyButtonStates();
            _ = InitializeAsync();
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save product: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        if (SelectedProduct != null) FillFields(SelectedProduct);
        StatusMessage = string.Empty;
    }

    [RelayCommand] private void Find() { ShowList = !ShowList; FilterProducts(); }
    [RelayCommand] private void List() { ShowList = true; FilterProducts(); }
    [RelayCommand] private void CloseList() => ShowList = false;

    [RelayCommand]
    private void SelectFromList(Product? m)
    {
        if (m == null) return;
        SelectedProduct = m;
        FillFields(m);
        ShowList = false;
    }

    partial void OnSearchTermChanged(string value) => FilterProducts();

    // ── Return Commands ────────────────────────────────────────────────
    /// <summary>Open modal to return a product back FROM a patient (stock increases, revenue decreases).</summary>
    [RelayCommand]
    private void OpenPatientReturn(Product product)
    {
        if (product == null) return;
        ReturnTargetProduct = product;
        ReturnType = "Patient Return";
        ReturnModalTitle = "Patient Medicine Return";
        ReturnQuantity = 1;
        ReturnReason = "Patient Changed Mind";
        OnPropertyChanged(nameof(ReturnModalSubtitle));
        OnPropertyChanged(nameof(ReturnReasons));
        StatusMessage = string.Empty;
        IsReturnModalOpen = true;
    }

    /// <summary>Open modal to return expired/unsold product TO supplier (stock decreases, revenue increases).</summary>
    [RelayCommand]
    private void OpenSupplierReturn(Product product)
    {
        if (product == null) return;
        ReturnTargetProduct = product;
        ReturnType = "Supplier Return";
        ReturnModalTitle = "Return to Seller";
        ReturnQuantity = 1;
        ReturnReason = "Expired";
        OnPropertyChanged(nameof(ReturnModalSubtitle));
        OnPropertyChanged(nameof(ReturnReasons));
        StatusMessage = string.Empty;
        IsReturnModalOpen = true;
    }

    [RelayCommand]
    private void CloseReturnModal()
    {
        IsReturnModalOpen = false;
        ReturnTargetProduct = null;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitReturnAsync()
    {
        if (ReturnTargetProduct == null) { StatusMessage = "No product selected."; return; }
        if (ReturnQuantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }

        if (ReturnType == "Supplier Return" && ReturnQuantity > ReturnTargetProduct.Stock)
        {
            StatusMessage = $"Cannot return more than available stock ({ReturnTargetProduct.Stock}).";
            return;
        }

        decimal unitPrice = ReturnType == "Patient Return"
            ? ReturnTargetProduct.SellingPrice   // refund patient at selling price
            : ReturnTargetProduct.PurchasePrice; // seller refunds at purchase price

        decimal refundAmount = unitPrice * ReturnQuantity;

        var ret = new ProductReturn
        {
            ReturnNo    = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
            ProductId   = ReturnTargetProduct.ProductID,
            BatchNo     = ReturnTargetProduct.BatchNumber ?? string.Empty,
            Quantity    = ReturnQuantity,
            ReturnType  = ReturnType,
            Reason      = ReturnReason,
            RefundAmount = refundAmount,
            CreatedBy   = CurrentUser?.UserID,
            CreatedAt   = DateTime.Now
        };

        try
        {
            await Task.Run(() => _returnRepo.Insert(ret));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var direction = ReturnType == "Patient Return"
                    ? $"Refund: Rs. {refundAmount:N2} | Stock +{ReturnQuantity}"
                    : $"Seller refunds: Rs. {refundAmount:N2} | Stock -{ReturnQuantity}";
                StatusMessage = $"Return processed. {direction}";
                LogActivity(ReturnType, $"{ReturnType}: {ReturnQuantity}x {ReturnTargetProduct.Name} — {ReturnReason}", "Returns");
                IsReturnModalOpen = false;
                ReturnTargetProduct = null;
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
                _ = InitializeAsync();
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "Failed: " + ex.Message);
        }
    }


    private void FilterProducts()
    {
        IEnumerable<Product> source = SelectedTab switch
        {
            1 => Products.Where(m => m.IsExpired),
            2 => Products.Where(m => !m.IsExpired && m.Stock > 0), // unsold = has stock, not expired, no sales reduction (approximate by stock > 0)
            _ => Products
        };

        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredProducts = new ObservableCollection<Product>(source);
        else
        {
            var t = SearchTerm.ToLower();
            FilteredProducts = new ObservableCollection<Product>(
                source.Where(m =>
                    m.Name.ToLower().Contains(t) ||
                    (m.GenericName?.ToLower().Contains(t) ?? false) ||
                    (m.CompanyName?.ToLower().Contains(t) ?? false) ||
                    (m.BatchNumber?.ToLower().Contains(t) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty;
        GenericName = string.Empty;
        Type = string.Empty;
        Category = string.Empty;
        Rack = string.Empty;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        PurchasePrice = "0.00";
        SellingPrice = "0.00";
        Stock = "0";
        MinimumStockLevel = "10";
        SelectedCompany = null;
        CompanyName = string.Empty;
        NotifyCalculatedTotals();
    }

    private void FillFields(Product m)
    {
        Name = m.Name;
        GenericName = m.GenericName ?? string.Empty;
        Type = m.Type ?? string.Empty;
        Category = m.Category ?? string.Empty;
        Rack = m.Rack ?? string.Empty;
        BatchNumber = m.BatchNumber ?? string.Empty;
        ExpiryDate = m.ExpiryDate.HasValue ? new DateTimeOffset(m.ExpiryDate.Value, TimeSpan.Zero) : null;
        PurchasePrice = m.PurchasePrice.ToString("F2");
        SellingPrice = m.SellingPrice.ToString("F2");
        Stock = m.Stock.ToString();
        MinimumStockLevel = m.MinimumStockLevel.ToString();
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == m.CompanyID);
        CompanyName = m.CompanyName ?? string.Empty;
        NotifyCalculatedTotals();
    }

    private Product BuildProduct() => new()
    {
        Name = Name,
        GenericName = string.IsNullOrWhiteSpace(GenericName) ? null : GenericName.Trim(),
        Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
        Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
        Rack = string.IsNullOrWhiteSpace(Rack) ? null : Rack.Trim(),
        BatchNumber = string.IsNullOrWhiteSpace(BatchNumber) ? null : BatchNumber.Trim(),
        ExpiryDate = ExpiryDate?.Date,
        PurchasePrice = decimal.TryParse(PurchasePrice, out var bp) ? bp : 0,
        SellingPrice = decimal.TryParse(SellingPrice, out var sp) ? sp : 0,
        Stock = int.TryParse(Stock, out var s) ? s : 0,
        MinimumStockLevel = int.TryParse(MinimumStockLevel, out var ms) ? ms : 10,
        CompanyID = SelectedCompany?.CompanyID,
        CompanyName = SelectedCompany != null ? SelectedCompany.Name : (string.IsNullOrWhiteSpace(CompanyName) ? null : CompanyName.Trim())
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    partial void OnStockChanged(string value) => NotifyCalculatedTotals();
    partial void OnPurchasePriceChanged(string value) => NotifyCalculatedTotals();
    partial void OnSellingPriceChanged(string value) => NotifyCalculatedTotals();

    private void NotifyCalculatedTotals()
    {
        OnPropertyChanged(nameof(ProfitPerUnitText));
        OnPropertyChanged(nameof(InventoryValueText));
    }

    private static string FormatMoney(decimal value) => $"Rs. {value:N2}";

    public async Task InitializeAsync()
    {
        try
        {
            var meds = await Task.Run(() => _repo.GetAll());
            var comps = await Task.Run(() => _companyRepo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = string.Empty;
                Companies = new ObservableCollection<Company>(comps);
                Products = new ObservableCollection<Product>(meds);
                FilterProducts();

                LowStockCount   = Products.Count(m => m.IsLowStock && !m.IsExpired);
                ExpiredCount     = Products.Count(m => m.IsExpired);
                var totalVal     = Products.Sum(m => m.Stock * m.SellingPrice);
                TotalInventoryValue = FormatMoney(totalVal);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load products: {ex.Message}");
        }
    }
}
