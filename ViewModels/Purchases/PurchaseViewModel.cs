using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Purchases;

public partial class PurchaseViewModel : ViewModelBase
{
    private readonly PurchaseRepository _repo;
    private readonly SupplierRepository _supplierRepo;
    private readonly ProductRepository _productRepo;

    public PurchaseViewModel(PurchaseRepository repo, SupplierRepository supplierRepo, ProductRepository productRepo)
    {
        _repo = repo;
        _supplierRepo = supplierRepo;
        _productRepo = productRepo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Purchase> _purchases = new();
    [ObservableProperty]
    private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty]
    private ObservableCollection<Product> _products = new();
    [ObservableProperty]
    private Purchase? _selectedPurchase;

    // Header Fields
    [ObservableProperty]
    private string _invoiceNumber = string.Empty;
    [ObservableProperty]
    private Supplier? _selectedSupplier;
    [ObservableProperty]
    private DateTimeOffset _purchaseDate = DateTimeOffset.Now;

    // Line Items for the current purchase
    [ObservableProperty]
    private ObservableCollection<PurchaseItem> _lineItems = new();
    
    // Line Item Input
    [ObservableProperty]
    private Product? _selectedProduct;
    [ObservableProperty]
    private string _batchNumber = string.Empty;
    [ObservableProperty]
    private DateTimeOffset? _expiryDate;
    [ObservableProperty]
    private int _quantity = 1;
    [ObservableProperty]
    private decimal _purchasePrice;
    [ObservableProperty]
    private decimal _discount;
    [ObservableProperty]
    private decimal _tax;

    public decimal GrandTotal => LineItems.Sum(x => x.Quantity * x.PurchasePrice - x.Discount + x.Tax); // Simplify for now

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Create new purchase invoice.";
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedPurchase == null) { StatusMessage = "Select a purchase first."; return; }
        
        InvoiceNumber = SelectedPurchase.InvoiceNumber;
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierID == SelectedPurchase.SupplierID);
        PurchaseDate = new DateTimeOffset(SelectedPurchase.PurchaseDate);
        
        var purchaseWithItems = await Task.Run(() => _repo.GetByIdWithItems(SelectedPurchase.PurchaseID));
        var items = purchaseWithItems?.Items ?? new List<PurchaseItem>();
        LineItems = new ObservableCollection<PurchaseItem>(items);
        
        OnPropertyChanged(nameof(GrandTotal));
        
        Mode = FormMode.View; // Still view mode, just showing details
        NotifyButtonStates();
        StatusMessage = $"Viewing details for {InvoiceNumber}";
    }

    [RelayCommand]
    private void AddLineItem()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }

        var item = new PurchaseItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            BatchNumber = BatchNumber,
            ExpiryDate = ExpiryDate?.DateTime,
            Quantity = Quantity,
            PurchasePrice = PurchasePrice,
            Discount = Discount,
            Tax = Tax
        };

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        
        // Reset inputs
        SelectedProduct = null;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        Quantity = 1;
        PurchasePrice = 0;
        Discount = 0;
        Tax = 0;
    }

    [RelayCommand]
    private void RemoveLineItem(PurchaseItem item)
    {
        if (item != null && LineItems.Contains(item))
        {
            LineItems.Remove(item);
            OnPropertyChanged(nameof(GrandTotal));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSupplier == null)
        {
            StatusMessage = "Supplier is required.";
            return;
        }

        if (!LineItems.Any())
        {
            StatusMessage = "At least one item is required.";
            return;
        }

        var p = new Purchase
        {
            InvoiceNumber = InvoiceNumber,
            PurchaseDate = PurchaseDate.DateTime,
            SupplierID = SelectedSupplier.SupplierID,
            TotalAmount = GrandTotal,
            Items = LineItems.ToList()
        };

        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(p));
            StatusMessage = "Purchase created and stock updated.";
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
        var suppliers = await Task.Run(() => _supplierRepo.GetAll());
        var products = await Task.Run(() => _productRepo.GetAll());
        var purchases = await Task.Run(() => _repo.GetAll());
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            Suppliers = new ObservableCollection<Supplier>(suppliers);
            Products = new ObservableCollection<Product>(products);
            Purchases = new ObservableCollection<Purchase>(purchases.OrderByDescending(p => p.PurchaseDate));
        });
    }

    private void ClearFields()
    {
        InvoiceNumber = string.Empty;
        SelectedSupplier = null;
        PurchaseDate = DateTimeOffset.Now;
        LineItems.Clear();
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    // Auto-fill price when product selected
    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
        {
            PurchasePrice = value.PurchaseRate;
            Tax = value.Tax;
        }
    }
}
