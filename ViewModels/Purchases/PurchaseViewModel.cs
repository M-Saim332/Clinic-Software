using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

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

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showForm;
    
    [ObservableProperty] private ObservableCollection<Purchase> _purchases = new();
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private Purchase? _selectedPurchase;

    // KPI Summary counts
    [ObservableProperty] private int _totalInvoicesCount;
    [ObservableProperty] private decimal _totalPurchasesAmount;
    [ObservableProperty] private int _totalSuppliersCount;
    [ObservableProperty] private decimal _averageInvoiceValue;

    // Header Fields
    [ObservableProperty] private string _invoiceNumber = string.Empty;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private string _supplierName = string.Empty;
    [ObservableProperty] private DateTimeOffset _purchaseDate = DateTimeOffset.Now;

    // Line Items for the current purchase
    [ObservableProperty] private ObservableCollection<PurchaseItem> _lineItems = new();
    
    // Line Item Input
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _batchNumber = string.Empty;
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;

    public decimal GrandTotal => LineItems.Sum(x => x.Quantity * x.PurchasePrice - x.Discount + x.Tax);

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && Mode == FormMode.Add;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        InvoiceNumber = "Auto-generated";
        Mode = FormMode.Add;
        ShowForm = true;
        NotifyButtonStates();
        StatusMessage = "Create new purchase invoice.";
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedPurchase == null) { StatusMessage = "Select a purchase first."; return; }
        
        try
        {
            InvoiceNumber = SelectedPurchase.InvoiceNumber;
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierID == SelectedPurchase.SupplierID);
            SupplierName = SelectedPurchase.SupplierName ?? string.Empty;
            PurchaseDate = new DateTimeOffset(SelectedPurchase.PurchaseDate);
            
            var purchaseWithItems = await Task.Run(() => _repo.GetByIdWithItems(SelectedPurchase.PurchaseID));
            var items = purchaseWithItems?.Items ?? new List<PurchaseItem>();
            LineItems = new ObservableCollection<PurchaseItem>(items);
            
            OnPropertyChanged(nameof(GrandTotal));
            
            Mode = FormMode.View;
            ShowForm = true;
            NotifyButtonStates();
            StatusMessage = $"Viewing details for {InvoiceNumber}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading purchase details: {ex.Message}";
        }
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
        StatusMessage = string.Empty;
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
        if (SelectedSupplier == null && string.IsNullOrWhiteSpace(SupplierName))
        {
            StatusMessage = "Supplier (or Supplier Name) is required.";
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
            SupplierID = SelectedSupplier?.SupplierID,
            SupplierName = SelectedSupplier == null ? SupplierName : null,
            TotalAmount = GrandTotal,
            Items = LineItems.ToList()
        };

        try
        {
            if (Mode == FormMode.Add)
            {
                await Task.Run(() => _repo.Insert(p));
                StatusMessage = "Purchase created and stock updated.";
                var supplierLabel = SelectedSupplier?.Name ?? SupplierName;
                LogActivity("Purchase Created", $"Invoice #{p.InvoiceNumber} from {supplierLabel} — Rs. {p.TotalAmount:N2}", "Purchases");
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            }
            
            Mode = FormMode.View;
            ShowForm = false;
            NotifyButtonStates();
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving invoice: {ex.Message}";
        }
    }

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
            var suppliers = await Task.Run(() => _supplierRepo.GetAll());
            var products = await Task.Run(() => _productRepo.GetAll());
            var purchases = await Task.Run(() => _repo.GetAll());
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                Suppliers = new ObservableCollection<Supplier>(suppliers);
                Products = new ObservableCollection<Product>(products);
                Purchases = new ObservableCollection<Purchase>(purchases.OrderByDescending(p => p.PurchaseDate));

                TotalInvoicesCount = Purchases.Count;
                TotalPurchasesAmount = Purchases.Sum(p => p.TotalAmount);
                TotalSuppliersCount = Suppliers.Count;
                AverageInvoiceValue = TotalInvoicesCount > 0 ? TotalPurchasesAmount / TotalInvoicesCount : 0;
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
        SelectedSupplier = null;
        SupplierName = string.Empty;
        PurchaseDate = DateTimeOffset.Now;
        LineItems.Clear();
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
        {
            PurchasePrice = value.PurchasePrice;
            Tax = 0;
        }
    }
}
