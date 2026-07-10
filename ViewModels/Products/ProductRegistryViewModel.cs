using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Products;

public partial class ProductRegistryViewModel : ViewModelBase
{
    private readonly ProductRepository _repo;
    private readonly CompanyRepository _companyRepo;

    public ProductRegistryViewModel(ProductRepository repo, CompanyRepository companyRepo)
    {
        _repo = repo;
        _companyRepo = companyRepo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Product> _products = new();
    [ObservableProperty]
    private ObservableCollection<Company> _companies = new();
    [ObservableProperty]
    private Product? _selectedProduct;

    // KPI
    [ObservableProperty] private int _inStockCount;
    [ObservableProperty] private int _lowStockCount;

    // Fields
    [ObservableProperty]
    private string _name = string.Empty;
    [ObservableProperty]
    private Company? _selectedCompany;
    [ObservableProperty]
    private decimal _purchaseRate;
    [ObservableProperty]
    private decimal _sellingPrice;
    [ObservableProperty]
    private decimal _tax;
    [ObservableProperty]
    private int _stockQuantity;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

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

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product first."; return; }
        FillFields(SelectedProduct);
        Mode = FormMode.Edit;
        NotifyButtonStates();
        var comps = await Task.Run(() => _companyRepo.GetAll());
        Companies = new ObservableCollection<Company>(comps);
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == SelectedProduct.CompanyID);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedProduct.ProductID));
        StatusMessage = ok ? "Product deleted." : "Cannot delete – referenced by purchases/sales.";
        if (ok)
        {
            SelectedProduct = null;
            await InitializeAsync();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || SelectedCompany == null) 
        { 
            StatusMessage = "Name and Company are required."; 
            return; 
        }

        var p = new Product 
        { 
            Name = Name, 
            CompanyID = SelectedCompany.CompanyID,
            PurchaseRate = PurchaseRate,
            SellingPrice = SellingPrice,
            Tax = Tax,
            StockQuantity = Mode == FormMode.Add ? StockQuantity : SelectedProduct!.StockQuantity
        };

        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(p));
            StatusMessage = "Product created.";
        }
        else
        {
            p.ProductID = SelectedProduct!.ProductID;
            await Task.Run(() => _repo.Update(p));
            StatusMessage = "Product updated.";
        }
        Mode = FormMode.View;
        NotifyButtonStates();
        await InitializeAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = string.Empty;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var companies = await Task.Run(() => _companyRepo.GetAll());
            var products = await Task.Run(() => _repo.GetAll());

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Companies = new ObservableCollection<Company>(companies);
                Products = new ObservableCollection<Product>(products);
                InStockCount = Products.Count(p => p.StockQuantity > 10);
                LowStockCount = Products.Count(p => p.StockQuantity <= 10 && p.StockQuantity > 0);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load products: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; 
        SelectedCompany = null; 
        PurchaseRate = 0; 
        SellingPrice = 0; 
        Tax = 0; 
        StockQuantity = 0;
    }

    private void FillFields(Product p)
    {
        Name = p.Name; 
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == p.CompanyID);
        PurchaseRate = p.PurchaseRate;
        SellingPrice = p.SellingPrice;
        Tax = p.Tax;
        StockQuantity = p.StockQuantity;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }
}
