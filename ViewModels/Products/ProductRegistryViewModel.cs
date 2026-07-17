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

    public ProductRegistryViewModel(ProductRepository repo, CompanyRepository companyRepo)
    {
        _repo = repo;
        _companyRepo = companyRepo;
    }

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    // ── KPI Summary Card properties ────────────────────────────────────
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private string _totalInventoryValue = "Rs. 0.00";

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

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
        Mode = FormMode.View;
        NotifyButtonStates();
        _ = InitializeAsync();
        WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
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



    private void FilterProducts()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredProducts = new ObservableCollection<Product>(Products);
        else
        {
            var t = SearchTerm.ToLower();
            FilteredProducts = new ObservableCollection<Product>(
                Products.Where(m =>
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
