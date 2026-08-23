using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Core.Enums;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Purchases;

public partial class PurchaseViewModel : ViewModelBase, ISearchable, INavigationContext
{
    public event Action? RequestAddSupplier;
    public event Action? RequestAddProduct;
    public int? PreselectedEntityId { get; set; }
    public Action<int>? ReturnToCaller { get; set; }
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
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    private bool _showForm;
    
    [ObservableProperty] private ObservableCollection<Purchase> _purchases = new();
    private ObservableCollection<Purchase> _allPurchases = new();
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private Purchase? _selectedPurchase;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Purchases...";

    partial void OnSearchTermChanged(string value) => FilterPurchases();

    private void FilterPurchases()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Purchases = new ObservableCollection<Purchase>(_allPurchases);
        }
        else
        {
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            Purchases = new ObservableCollection<Purchase>(
                _allPurchases.Where(p => 
                    p.InvoiceNumber.ToLower().Contains(term) ||
                    (p.SupplierName?.ToLower().Contains(term) ?? false)));
        }
    }

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
    [ObservableProperty] private int _bonusQuantity;
    [ObservableProperty] private string _packageType = "Box";
    [ObservableProperty] private int _unitsPerPackage = 1;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _packMRP;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _extraDiscount;
    [ObservableProperty] private decimal _aTax;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraftInvoice))]
    [NotifyPropertyChangedFor(nameof(IsCheckingInvoice))]
    [NotifyPropertyChangedFor(nameof(IsPostedInvoice))]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    private InvoiceState _invoiceState = InvoiceState.Draft;
    private int _currentPurchaseId;

    public List<string> PackageTypes { get; } = new() { "Box", "Carton", "Pack", "Bottle", "Piece" };
    public int TotalUnitsToStock => (Quantity + BonusQuantity) * Math.Max(1, UnitsPerPackage);
    public string LoggedInUserName => CurrentUser?.DisplayName ?? "Unknown";

    public decimal GrandTotal => LineItems.Sum(x => x.LineNetTotal);
    public decimal EffectiveRate => Math.Round(PurchasePrice * 0.85m, 2);
    public decimal UnitMRP => UnitsPerPackage > 0 ? Math.Round(PackMRP / UnitsPerPackage, 2, MidpointRounding.AwayFromZero) : 0;
    public bool IsDraftInvoice => InvoiceState == InvoiceState.Draft;
    public bool IsCheckingInvoice => InvoiceState == InvoiceState.Checking;
    public bool IsPostedInvoice => InvoiceState == InvoiceState.Posted;
    public bool CanEditDraft => ShowForm && Mode == FormMode.View && InvoiceState == InvoiceState.Draft && _currentPurchaseId > 0;
    public bool CanCheckInvoice => CanEditDraft;

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && (Mode == FormMode.Add || Mode == FormMode.Edit);

    [RelayCommand]
    private async Task NewAsync()
    {
        ClearFields();
        InvoiceNumber = await Task.Run(_repo.GetNextInvoiceNumber);
        InvoiceState = InvoiceState.Draft;
        _currentPurchaseId = 0;

        // Ensure suppliers and products are loaded before showing the form
        try
        {
            var suppliers = await Task.Run(() => _supplierRepo.GetAll());
            var products  = await Task.Run(() => _productRepo.GetAll());
            Suppliers = new System.Collections.ObjectModel.ObservableCollection<Supplier>(suppliers);
            Products  = new System.Collections.ObjectModel.ObservableCollection<Product>(products);
        }
        catch { /* silently keep existing lists if refresh fails */ }

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
            _currentPurchaseId = SelectedPurchase.PurchaseID;
            InvoiceState = purchaseWithItems?.IsPosted == true ? InvoiceState.Posted : InvoiceState.Draft;
            
            OnPropertyChanged(nameof(GrandTotal));
            
            Mode = FormMode.View;
            ShowForm = true;
            NotifyButtonStates();
            StatusMessage = $"Viewing invoice {InvoiceNumber}";
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
        if (!ExpiryDate.HasValue) { StatusMessage = "Expiry date is required."; return; }
        if (BonusQuantity < 0 || UnitsPerPackage <= 0) { StatusMessage = "Bonus and units-per-package values are invalid."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateDiscountPercentage(Discount)) { StatusMessage = "Discount must be between 0% and 100%."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateDiscountPercentage(ExtraDiscount)) { StatusMessage = "Extra discount must be between 0% and 100%."; return; }
        if (ATax < 0 || ATax > 100) { StatusMessage = "Additional tax must be between 0% and 100%."; return; }

        // Compute line total using percentage-based discount and tax
        var item = new PurchaseItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            BatchNumber = BatchNumber,
            ExpiryDate = ExpiryDate?.DateTime,
            PackageQuantity = Quantity,
            BonusQuantity = BonusQuantity,
            PackageType = PackageType,
            UnitsPerPackage = UnitsPerPackage,
            Quantity = TotalUnitsToStock,
            PurchasePrice = PurchasePrice,
            PackMRP = PackMRP,
            Discount = Discount,
            Tax = Tax,
            ExtraDiscount = ExtraDiscount,
            ATax = ATax
        };
        // Align stored TotalAmount with the same % formula

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        
        // Reset inputs
        SelectedProduct = null;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        Quantity = 1;
        BonusQuantity = 0;
        PackageType = "Box";
        UnitsPerPackage = 1;
        PurchasePrice = 0;
        PackMRP = 0;
        Discount = 0;
        Tax = 0;
        ExtraDiscount = 0;
        ATax = 0;
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

        // Auto-add line item if the user forgot to click "+ Add"
        if (SelectedProduct != null)
        {
            AddLineItem();
            if (!string.IsNullOrEmpty(StatusMessage)) return; // If AddLineItem failed validation, stop saving
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
            CreatedBy = CurrentUser?.UserID,
            CreatedByName = LoggedInUserName,
            Items = LineItems.ToList()
        };

        try
        {
            if (Mode == FormMode.Add)
            {
                _currentPurchaseId = await Task.Run(() => _repo.Insert(p));
                p.PurchaseID = _currentPurchaseId;
                InvoiceNumber = p.InvoiceNumber;
                SelectedPurchase = p;
                StatusMessage = "Draft saved. Check the invoice before posting; stock is unchanged.";
                var supplierLabel = SelectedSupplier?.Name ?? SupplierName;
                LogActivity("Purchase Created", $"Invoice #{p.InvoiceNumber} from {supplierLabel} — Rs. {p.TotalAmount:N2}", "Purchases");
            }
            else if (Mode == FormMode.Edit)
            {
                p.PurchaseID = _currentPurchaseId;
                await Task.Run(() => _repo.Update(p));
                SelectedPurchase = p;
                StatusMessage = "Draft updated. Check the invoice before posting.";
                LogActivity("Purchase Updated", $"Draft invoice #{p.InvoiceNumber} updated", "Purchases");
            }

            await InitializeAsync();
            Mode = FormMode.View;
            ShowForm = true;
            NotifyButtonStates();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving invoice: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAndCheckAsync()
    {
        await SaveAsync();
        if (Mode == FormMode.View && InvoiceState == InvoiceState.Draft && _currentPurchaseId > 0)
        {
            CheckInvoice();
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
                var sorted = new ObservableCollection<Purchase>(purchases.OrderByDescending(p => p.PurchaseDate));
                _allPurchases = sorted;
                FilterPurchases();

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
        Quantity = 1;
        BonusQuantity = 0;
        PackageType = "Box";
        UnitsPerPackage = 1;
        LineItems.Clear();
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(CanEditDraft));
        OnPropertyChanged(nameof(CanCheckInvoice));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
        {
            PurchasePrice = value.PurchasePrice;
            PackMRP = value.SellingPrice;
            UnitsPerPackage = Math.Max(1, value.TabletsPerBox);
            Tax = 0;
        }
    }

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(TotalUnitsToStock));
    partial void OnBonusQuantityChanged(int value) => OnPropertyChanged(nameof(TotalUnitsToStock));
    partial void OnUnitsPerPackageChanged(int value) 
    {
        OnPropertyChanged(nameof(TotalUnitsToStock));
        OnPropertyChanged(nameof(UnitMRP));
    }
    partial void OnPurchasePriceChanged(decimal value) => OnPropertyChanged(nameof(EffectiveRate));
    partial void OnPackMRPChanged(decimal value) => OnPropertyChanged(nameof(UnitMRP));
    [RelayCommand] private void QuickAddSupplier() => RequestAddSupplier?.Invoke();
    [RelayCommand] private void QuickAddProduct() => RequestAddProduct?.Invoke();

    public async Task PreselectSupplierAsync(int id)
    {
        Suppliers = new ObservableCollection<Supplier>(await Task.Run(_supplierRepo.GetAll));
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierID == id); PreselectedEntityId = id;
    }

    public async Task PreselectProductAsync(int id)
    {
        Products = new ObservableCollection<Product>(await Task.Run(_productRepo.GetAll));
        SelectedProduct = Products.FirstOrDefault(p => p.ProductID == id); PreselectedEntityId = id;
    }

    [RelayCommand]
    private void CheckInvoice()
    {
        if (_currentPurchaseId == 0 && SelectedPurchase?.PurchaseID > 0) _currentPurchaseId = SelectedPurchase.PurchaseID;
        if (_currentPurchaseId == 0) { StatusMessage = "Save the draft before checking it."; return; }
        InvoiceState = InvoiceState.Checking;
        StatusMessage = "Invoice checked. Review it, then post.";
    }

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        if (InvoiceState != InvoiceState.Checking) { CheckInvoice(); return; }
        var id = _currentPurchaseId > 0 ? _currentPurchaseId : SelectedPurchase?.PurchaseID ?? 0;
        if (id == 0) { StatusMessage = "Save and check the invoice first."; return; }
        try
        {
            await Task.Run(() => _repo.PostPurchase(id));
            InvoiceState = InvoiceState.Posted;
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            StatusMessage = "Purchase posted. Stock and product rates were updated.";
            await InitializeAsync();
        }
        catch (Exception ex) { StatusMessage = $"Posting failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task PostAndAddAnotherAsync()
    {
        await PostInvoiceAsync();
        if (InvoiceState == InvoiceState.Posted) await NewAsync();
    }

    [RelayCommand]
    private void EditCheckedInvoice()
    {
        if (IsPostedInvoice) return;
        InvoiceState = InvoiceState.Draft;
        if (_currentPurchaseId > 0) Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Invoice returned to draft editing.";
    }
    [RelayCommand]
    private void EditDraft()
    {
        if (!CanEditDraft) return;
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Editing draft invoice.";
    }
}
