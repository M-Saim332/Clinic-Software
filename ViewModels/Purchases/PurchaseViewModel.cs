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
    private const decimal DefaultExtraDiscountPercent = 15m;

    public PurchaseViewModel(PurchaseRepository repo, SupplierRepository supplierRepo, ProductRepository productRepo)
    {
        _repo = repo;
        _supplierRepo = supplierRepo;
        _productRepo = productRepo;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    [NotifyPropertyChangedFor(nameof(IsViewingInvoiceDocument))]
    private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    [NotifyPropertyChangedFor(nameof(IsViewingInvoiceDocument))]
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
    [ObservableProperty] private string _supplierSearchText = string.Empty;
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
    [ObservableProperty] private decimal? _purchasePrice;
    [ObservableProperty] private decimal _packMRP;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _extraDiscount = DefaultExtraDiscountPercent;
    [ObservableProperty] private decimal _aTax;
    [ObservableProperty] private decimal _companySalesTax;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraftInvoice))]
    [NotifyPropertyChangedFor(nameof(IsCheckingInvoice))]
    [NotifyPropertyChangedFor(nameof(IsPostedInvoice))]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    [NotifyPropertyChangedFor(nameof(CanCheckInvoice))]
    private InvoiceState _invoiceState = InvoiceState.Draft;
    private int _currentPurchaseId;

    public List<string> PackageTypes { get; } = new() { "Box", "Carton", "Pack", "Bottle", "Piece" };
    public int TotalUnitsToStock => Quantity + BonusQuantity;
    public string LoggedInUserName => CurrentUser?.DisplayName ?? "Unknown";

    public decimal GrandTotal => LineItems.Sum(x => x.LineNetTotal);
    public decimal DocumentSubTotal => LineItems.Sum(x => x.SubTotal);
    public decimal DocumentTaxTotal => LineItems.Sum(x => x.TaxableOverhead);
    public decimal DocumentDiscountTotal => LineItems.Sum(x => x.DiscountedValue + x.ExtraDiscountedValue);
    public decimal DocumentAdjustmentsTotal => DocumentTaxTotal - DocumentDiscountTotal;
    public int DocumentTotalItems => LineItems.Count;
    public int DocumentTotalQuantity => LineItems.Sum(x => x.PackageQuantity);
    public decimal DocumentGrossAmount => LineItems.Sum(x => x.GrossLineAmount);
    public decimal DocumentDiscountAmount => LineItems.Sum(x => x.DiscountAmount);
    public decimal DocumentAdvanceWHTax => LineItems.Sum(x => x.AdvTaxAmount);
    public decimal DocumentTaxAmount => LineItems.Sum(x => x.TaxAmount);
    public decimal DocumentCNValue => 0;
    public decimal DocumentNetTotal => GrandTotal;
    public string DocumentGeneratedDateDisplay => DateTime.Now.ToString("dd MMM yyyy hh:mm tt");
    private decimal RateInput => PurchasePrice ?? 0m;
    public decimal EffectiveRate => RateInput;
    public decimal LineGrossAmountPreview => Quantity * RateInput;
    public decimal LineDiscountAmountPreview => LineGrossAmountPreview * (ExtraDiscount / 100m);
    public decimal LineSubTotalPreview => Math.Max(0, LineGrossAmountPreview - LineDiscountAmountPreview);
    public decimal LineAdvanceTaxPreview => LineSubTotalPreview * (ATax / 100m);
    public decimal LineCompanySalesTaxPreview => LineSubTotalPreview * (CompanySalesTax / 100m);
    public decimal LineTaxTotalPreview => LineAdvanceTaxPreview + LineCompanySalesTaxPreview;
    public decimal LineNetTotalPreview => LineSubTotalPreview + LineAdvanceTaxPreview + LineCompanySalesTaxPreview;
    public bool IsDraftInvoice => InvoiceState == InvoiceState.Draft;
    public bool IsCheckingInvoice => InvoiceState == InvoiceState.Checking;
    public bool IsPostedInvoice => InvoiceState == InvoiceState.Posted;
    public bool CanEditDraft => ShowForm && Mode == FormMode.View && InvoiceState == InvoiceState.Draft && _currentPurchaseId > 0;
    public bool CanCheckInvoice => CanEditDraft;

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && (Mode == FormMode.Add || Mode == FormMode.Edit);
    public bool IsViewingInvoiceDocument => ShowForm && Mode == FormMode.View;
    public string DocumentStatus => IsPostedInvoice ? "POSTED" : IsCheckingInvoice ? "CHECKING" : "DRAFT";
    public string SupplierDisplayName => SelectedSupplier?.Name ?? SupplierName ?? "Walk-in / Unlisted Supplier";

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
            SupplierSearchText = SelectedSupplier?.Name ?? SupplierName;
            PurchaseDate = new DateTimeOffset(SelectedPurchase.PurchaseDate);
            
            var purchaseWithItems = await Task.Run(() => _repo.GetByIdWithItems(SelectedPurchase.PurchaseID));
            var items = purchaseWithItems?.Items ?? new List<PurchaseItem>();
            LineItems = new ObservableCollection<PurchaseItem>(items);
            _currentPurchaseId = SelectedPurchase.PurchaseID;
            InvoiceState = purchaseWithItems?.IsPosted == true ? InvoiceState.Posted : InvoiceState.Draft;
            
            OnPropertyChanged(nameof(GrandTotal));
            NotifyDocumentTotals();
            
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
        if (!PurchasePrice.HasValue || PurchasePrice.Value <= 0) { StatusMessage = "Enter the supplier Rate for this product."; return; }
        if (!ExpiryDate.HasValue) { StatusMessage = "Expiry date is required."; return; }
        if (BonusQuantity < 0) { StatusMessage = "Bonus quantity is invalid."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateDiscountPercentage(Discount)) { StatusMessage = "Discount must be between 0% and 100%."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateDiscountPercentage(ExtraDiscount)) { StatusMessage = "Extra discount must be between 0% and 100%."; return; }
        if (ATax < 0 || ATax > 100) { StatusMessage = "Advance tax must be between 0% and 100%."; return; }
        if (CompanySalesTax < 0 || CompanySalesTax > 100) { StatusMessage = "Company sales tax must be between 0% and 100%."; return; }

        var item = new PurchaseItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            BatchNumber = BatchNumber,
            ExpiryDate = ExpiryDate?.DateTime,
            PackageQuantity = Quantity,
            BonusQuantity = BonusQuantity,
            PackageType = PackageType,
            Quantity = TotalUnitsToStock,
            PurchasePrice = PurchasePrice.Value,
            PackMRP = PackMRP,
            Discount = Discount,
            Tax = Tax,
            ExtraDiscount = ExtraDiscount,
            ATax = ATax,
            CompanySalesTax = CompanySalesTax
        };

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        NotifyDocumentTotals();
        
        // Reset inputs
        SelectedProduct = null;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        Quantity = 1;
        BonusQuantity = 0;
        PackageType = "Box";
        PurchasePrice = null;
        PackMRP = 0;
        Discount = 0;
        Tax = 0;
        ExtraDiscount = DefaultExtraDiscountPercent;
        ATax = 0;
        CompanySalesTax = 0;
        NotifyLinePreviewTotals();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RemoveLineItem(PurchaseItem item)
    {
        if (item != null && LineItems.Contains(item))
        {
            LineItems.Remove(item);
            OnPropertyChanged(nameof(GrandTotal));
            NotifyDocumentTotals();
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
        SupplierSearchText = string.Empty;
        SupplierName = string.Empty;
        PurchaseDate = DateTimeOffset.Now;
        Quantity = 1;
        BonusQuantity = 0;
        PackageType = "Box";
        SelectedProduct = null;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        PurchasePrice = null;
        PackMRP = 0;
        Discount = 0;
        Tax = 0;
        ExtraDiscount = DefaultExtraDiscountPercent;
        ATax = 0;
        CompanySalesTax = 0;
        LineItems.Clear();
        OnPropertyChanged(nameof(GrandTotal));
        NotifyDocumentTotals();
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(CanEditDraft));
        OnPropertyChanged(nameof(CanCheckInvoice));
        OnPropertyChanged(nameof(IsViewingInvoiceDocument));
        OnPropertyChanged(nameof(DocumentStatus));
    }

    private void NotifyDocumentTotals()
    {
        OnPropertyChanged(nameof(DocumentSubTotal));
        OnPropertyChanged(nameof(DocumentTaxTotal));
        OnPropertyChanged(nameof(DocumentDiscountTotal));
        OnPropertyChanged(nameof(DocumentAdjustmentsTotal));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(DocumentTotalItems));
        OnPropertyChanged(nameof(DocumentTotalQuantity));
        OnPropertyChanged(nameof(DocumentGrossAmount));
        OnPropertyChanged(nameof(DocumentDiscountAmount));
        OnPropertyChanged(nameof(DocumentAdvanceWHTax));
        OnPropertyChanged(nameof(DocumentTaxAmount));
        OnPropertyChanged(nameof(DocumentCNValue));
        OnPropertyChanged(nameof(DocumentNetTotal));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
        {
            PackMRP = value.MRP;
            PurchasePrice = null;
            ExtraDiscount = DefaultExtraDiscountPercent;
            Tax = 0;
            NotifyLinePreviewTotals();
        }
    }

    partial void OnSelectedSupplierChanged(Supplier? value)
    {
        if (value == null) return;
        SupplierName = string.Empty;
        SupplierSearchText = value.Name;
        OnPropertyChanged(nameof(SupplierDisplayName));
    }

    partial void OnSupplierSearchTextChanged(string value)
    {
        if (SelectedSupplier != null && !string.Equals(SelectedSupplier.Name, value, StringComparison.OrdinalIgnoreCase))
            SelectedSupplier = null;
        if (SelectedSupplier == null)
            SupplierName = value?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(SupplierDisplayName));
    }

    partial void OnQuantityChanged(int value) { OnPropertyChanged(nameof(TotalUnitsToStock)); NotifyLinePreviewTotals(); }
    partial void OnBonusQuantityChanged(int value) => OnPropertyChanged(nameof(TotalUnitsToStock));
    partial void OnPurchasePriceChanged(decimal? value) { OnPropertyChanged(nameof(EffectiveRate)); NotifyLinePreviewTotals(); }
    partial void OnPackMRPChanged(decimal value) => NotifyLinePreviewTotals();
    partial void OnExtraDiscountChanged(decimal value) => NotifyLinePreviewTotals();
    partial void OnATaxChanged(decimal value) => NotifyLinePreviewTotals();
    partial void OnCompanySalesTaxChanged(decimal value) => NotifyLinePreviewTotals();

    private void NotifyLinePreviewTotals()
    {
        OnPropertyChanged(nameof(LineSubTotalPreview));
        OnPropertyChanged(nameof(LineGrossAmountPreview));
        OnPropertyChanged(nameof(LineDiscountAmountPreview));
        OnPropertyChanged(nameof(LineAdvanceTaxPreview));
        OnPropertyChanged(nameof(LineCompanySalesTaxPreview));
        OnPropertyChanged(nameof(LineTaxTotalPreview));
        OnPropertyChanged(nameof(LineNetTotalPreview));
    }
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
        OnPropertyChanged(nameof(DocumentStatus));
        StatusMessage = "Invoice checked. Review it, then post.";
    }

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        if (SaveCancelEnabled)
        {
            await SaveAsync();
            if (_currentPurchaseId == 0 || Mode != FormMode.View) return;
        }

        var id = _currentPurchaseId > 0 ? _currentPurchaseId : SelectedPurchase?.PurchaseID ?? 0;
        if (id == 0) { StatusMessage = "Save the draft before posting stock."; return; }
        if (InvoiceState == InvoiceState.Posted) { StatusMessage = "Purchase invoice is already posted."; return; }
        try
        {
            await Task.Run(() => _repo.PostPurchase(id));
            InvoiceState = InvoiceState.Posted;
            OnPropertyChanged(nameof(DocumentStatus));
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
        OnPropertyChanged(nameof(DocumentStatus));
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

    [RelayCommand]
    private void PrintPurchaseDocument()
    {
        StatusMessage = "Purchase invoice is ready for print/export from the document preview.";
    }

    [RelayCommand]
    private void EmailPurchaseDocument()
    {
        StatusMessage = "Email document action is available from the invoice preview.";
    }
}
