using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Products;

public enum FormMode { View, Add, Edit }

public partial class ProductRegistryViewModel : ViewModelBase, ISearchable, INavigationContext
{
    public event Action? RequestAddCompany;
    public event Action<Product>? ProductSaved;
    public event Action? CancelRequested;
    public int? PreselectedEntityId { get; set; }
    public Action<int>? ReturnToCaller { get; set; }
    private readonly ProductRepository _repo;
    private readonly CompanyRepository _companyRepo;
    private readonly ReturnRepository _returnRepo;

    public ProductRegistryViewModel(ProductRepository repo, CompanyRepository companyRepo, ReturnRepository returnRepo)
    {
        _repo = repo;
        _companyRepo = companyRepo;
        _returnRepo = returnRepo;

        // Static handler avoids capturing this in a delegate; the messenger retains only a weak recipient reference.
        WeakReferenceMessenger.Default.Register<ProductRegistryViewModel, InventoryChangedMessage>(
            this, static (recipient, message) => _ = recipient.InitializeAsync());
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditInitialStock))]
    private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;
    private CancellationTokenSource? _searchCancellation;
    public string SearchPlaceholder => "Search Products...";

    // ── KPI Summary Card properties ────────────────────────────────────
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private string _totalInventoryValue = "Rs. 0.00";

    public string MutationEnabled_str => Mode.ToString();
    public bool CanManageProducts => CurrentUser?.IsAdmin == true || CurrentUser?.UserRole != ClinicSystem.Core.Enums.UserRole.Doctor;
    public bool MutationEnabled => Mode == FormMode.View && CanManageProducts;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool CanEditInitialStock => Mode == FormMode.Add;
    public string ProductCodeDisplay => PCode > 0 ? $"Product Code: {PCode}" : "Product Code: generating...";

    // ── Tab selection ──────────────────────────────────────────────────
    [ObservableProperty] private int _selectedTab = 0; // 0=All, 1=Expired, 2=Unsold
    partial void OnSelectedTabChanged(int value) => FilterProducts();

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Product> _filteredProducts = new();
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private ObservableCollection<ProductStockBatchDto> _productStockBatches = new();
    [ObservableProperty] private ObservableCollection<ProductStockBatchDto> _filteredProductStockBatches = new();
    [ObservableProperty] private ProductStockBatchDto? _selectedProductStockBatch;
    [ObservableProperty] private ProductStockBatchDto? _viewingStockBatch;
    [ObservableProperty] private bool _showBatchDetails;
    private int? _editingStockId;

    // Companies for selection ComboBox
    [ObservableProperty] private ObservableCollection<Company> _companies = new();
    [ObservableProperty] private Company? _selectedCompany;
    [ObservableProperty] private Company? _companyFilter;

    // Fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProductCodeDisplay))]
    private int _pCode;
    [ObservableProperty] private string _genericName = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _rack = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private string _rate = "0.00";
    [ObservableProperty] private string _purchasePrice = "0.00";
    [ObservableProperty] private string _tabletsPerBox = "1";
    [ObservableProperty] private string _initialQuantityPacks = "0";
    [ObservableProperty] private string _minimumStockLevel = "10";

    private decimal CurrentRate => decimal.TryParse(Rate, out var r) ? r : 0;
    private decimal CurrentPurchasePrice => decimal.TryParse(PurchasePrice, out var value) ? value : 0;
    private int CurrentTabletsPerBox => int.TryParse(TabletsPerBox, out var value) && value > 0 ? value : 1;
    private int CurrentInitialQuantityPacks => int.TryParse(InitialQuantityPacks, out var value) && value > 0 ? value : 0;
    // Live preview: MRP per piece
    public string PricePerTabletDisplay => FormatMoney(CurrentPurchasePrice / CurrentTabletsPerBox);
    // Live preview: Estimated landed cost = Rate / PiecesPerUnit
    public string EstimatedLandedCostDisplay =>
        CurrentRate > 0
            ? FormatMoney(Math.Round(CurrentRate / CurrentTabletsPerBox, 4))
            : "—";
    // Live preview: Estimated margin = MRP per piece − Landed cost per piece
    public string EstimatedMarginDisplay
    {
        get
        {
            if (CurrentRate <= 0) return "—";
            var mrpPerPc = CurrentPurchasePrice / CurrentTabletsPerBox;
            var landedPerPc = Math.Round(CurrentRate / CurrentTabletsPerBox, 4);
            var margin = mrpPerPc - landedPerPc;
            var sign = margin >= 0 ? "+" : string.Empty;
            return $"{sign}Rs. {margin:N2} / pc";
        }
    }
    public string InitialStockPiecesDisplay => CurrentTabletsPerBox > 1
        ? $"{CurrentInitialQuantityPacks * CurrentTabletsPerBox} pieces ({CurrentInitialQuantityPacks} packs × {CurrentTabletsPerBox} per pack)"
        : $"{CurrentInitialQuantityPacks} pieces";
    /// <summary>True when a Rate has been entered, enabling the COGS/Margin preview panel.</summary>
    public bool IsRatePreviewVisible => CurrentRate > 0;
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    // ── Delete confirmation state ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmationTitle))]
    private Product? _pendingDeleteProduct;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmationTitle))]
    private ProductStockBatchDto? _pendingDeleteStockBatch;
    [ObservableProperty] private bool _showDeleteConfirm;
    [ObservableProperty] private bool _deleteAllRequested;
    public string PendingDeleteLabel => DeleteAllRequested
        ? "All active products"
        : PendingDeleteStockBatch != null
            ? $"{PendingDeleteStockBatch.ProductName} — expires {PendingDeleteStockBatch.ExpiryDate:dd MMM yyyy}"
            : PendingDeleteProduct?.Name ?? string.Empty;
    public string DeleteConfirmationTitle => PendingDeleteStockBatch != null ? "Archive Stock Batch?" : "Delete Product?";

    // ── Return Modal State (Patient Return & Supplier Return) ──────────
    [ObservableProperty] private bool _isReturnModalOpen;
    [ObservableProperty] private string _returnModalTitle = string.Empty;
    [ObservableProperty] private string _returnType = "Patient Return"; // "Patient Return" or "Supplier Return"
    [ObservableProperty] private int _returnQuantity = 1;
    [ObservableProperty] private string _returnReason = "Patient Changed Mind";
    [ObservableProperty] private Product? _returnTargetProduct;
    public string ReturnModalSubtitle => ReturnTargetProduct != null
        ? $"Product: {ReturnTargetProduct.Name} | Stock: {ReturnTargetProduct.TotalStock}"
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
        RefreshNextProductCode();
    }

    // ── Row-level commands (match Patients pattern) ────────────────────
    [RelayCommand]
    private async Task EditSpecificAsync(Product product)
    {
        if (!CanManageProducts) { StatusMessage = "The doctor's drug inventory is read-only."; return; }
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
    private async Task ViewSpecificAsync(Product product)
    {
        if (product == null) return;
        SelectedProduct = product;
        var comps = await Task.Run(() => _companyRepo.GetAll());
        Companies = new ObservableCollection<Company>(comps);
        FillFields(product);
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == product.CompanyID);
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = "Viewing product details.";
    }

    [RelayCommand]
    private async Task EditBatchSpecificAsync(ProductStockBatchDto batch)
    {
        if (!CanManageProducts) { StatusMessage = "The doctor's drug inventory is read-only."; return; }
        if (batch == null) return;

        var product = await Task.Run(() => _repo.GetById(batch.ProductID));
        if (product == null) { StatusMessage = "The selected product was not found."; return; }

        SelectedProduct = product;
        SelectedProductStockBatch = batch;
        Companies = new ObservableCollection<Company>(await Task.Run(() => _companyRepo.GetAll()));
        FillFields(product);
        Rate = batch.RateTP.ToString("F2");
        PurchasePrice = batch.MRP.ToString("F2");
        TabletsPerBox = Math.Max(1, batch.PiecesPerUnit).ToString();
        ExpiryDate = new DateTimeOffset(batch.ExpiryDate, TimeSpan.Zero);
        _editingStockId = batch.StockID;
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Editing the selected expiry batch.";
    }

    [RelayCommand]
    private Task ViewBatchSpecificAsync(ProductStockBatchDto batch)
    {
        if (batch == null) return Task.CompletedTask;
        ViewingStockBatch = batch;
        ShowBatchDetails = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void RequestDeleteBatchSpecific(ProductStockBatchDto batch)
    {
        if (!CanManageProducts) { StatusMessage = "The doctor's drug inventory is read-only."; return; }
        if (batch == null) return;
        PendingDeleteProduct = null;
        PendingDeleteStockBatch = batch;
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private void OpenPatientReturnBatch(ProductStockBatchDto batch)
    {
        var product = GetProductForBatch(batch);
        if (product != null)
            OpenPatientReturn(product);
    }

    private Product? GetProductForBatch(ProductStockBatchDto? batch)
    {
        if (batch == null) return null;
        var product = Products.FirstOrDefault(item => item.ProductID == batch.ProductID);
        if (product == null) return null;

        // The detail form should reflect the exact batch the user selected.
        product.EarliestExpiry = batch.ExpiryDate;
        product.TotalStock = batch.StockQuantity;
        product.Rate = batch.RateTP;
        product.MRP = batch.MRP;
        return product;
    }

    [RelayCommand]
    private void RequestDeleteSpecific(Product product)
    {
        if (!CanManageProducts) { StatusMessage = "The doctor's drug inventory is read-only."; return; }
        if (product == null) return;
        PendingDeleteStockBatch = null;
        PendingDeleteProduct = product;
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var target = PendingDeleteProduct;
        var batch = PendingDeleteStockBatch;
        ShowDeleteConfirm = false;
        PendingDeleteProduct = null;
        PendingDeleteStockBatch = null;
        if (DeleteAllRequested)
        {
            DeleteAllRequested = false;
            if (!IsAdmin) { StatusMessage = "Only an administrator can archive all products."; return; }
            var count = await Task.Run(_repo.SoftDeleteAll);
            StatusMessage = $"{count} product(s) archived. Historical transactions were preserved.";
            LogActivity("Products Archived", $"Archived all {count} active products", "Products");
            await InitializeAsync();
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            return;
        }
        if (batch != null)
        {
            var archived = await Task.Run(() => _repo.ArchiveStockBatch(batch.StockID));
            if (archived)
            {
                StatusMessage = $"Archived batch expiring {batch.ExpiryDate:dd MMM yyyy}.";
                LogActivity("Stock Batch Archived", $"Archived {batch.ProductName} batch expiring {batch.ExpiryDate:dd MMM yyyy}", "Products");
                await InitializeAsync();
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            }
            else
            {
                StatusMessage = "The selected stock batch was not found.";
            }
            return;
        }

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
        PendingDeleteStockBatch = null;
        DeleteAllRequested = false;
    }

    [RelayCommand]
    private void CloseBatchDetails()
    {
        ShowBatchDetails = false;
        ViewingStockBatch = null;
    }

    [RelayCommand]
    private void RequestDeleteAll()
    {
        if (!IsAdmin) { StatusMessage = "Only an administrator can archive all products."; return; }
        DeleteAllRequested = true;
        PendingDeleteProduct = null;
        PendingDeleteStockBatch = null;
        ShowDeleteConfirm = true;
        OnPropertyChanged(nameof(PendingDeleteLabel));
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
        var isNewProduct = Mode == FormMode.Add;
        if (!ClinicSystem.UI.Helpers.ValidationHelper.IsValidName(Name)) { StatusMessage = "Valid Name is required (min 2 chars, no numbers)."; return; }
        if (isNewProduct && SelectedCompany == null) { StatusMessage = "Select a company before adding a product."; return; }
        if (!decimal.TryParse(PurchasePrice, out var purchase) || purchase < 0) { StatusMessage = "Enter a valid MRP."; return; }
        if (!int.TryParse(MinimumStockLevel, out var minStock) || minStock < 0) { StatusMessage = "Enter valid minimum stock."; return; }
        if (!int.TryParse(TabletsPerBox, out var tablets) || tablets <= 0) { StatusMessage = "Pieces per unit must be at least 1."; return; }
        var initialPacks = 0;
        if (isNewProduct && (!int.TryParse(InitialQuantityPacks, out initialPacks) || initialPacks < 0))
        {
            StatusMessage = "Initial quantity packs must be zero or greater.";
            return;
        }
        if (isNewProduct && initialPacks > 0 && ExpiryDate == null)
        {
            StatusMessage = "Expiry Date is required when adding initial stock.";
            return;
        }

        var m = BuildProduct();
        try
        {
            await Task.Run(() =>
            {
                if (isNewProduct)
                {
                    m.ProductID = _repo.Insert(m);
                    if (initialPacks > 0)
                    {
                        var stock = new ProductStock
                        {
                            ProductID = m.ProductID,
                            ExpiryDate = ExpiryDate?.DateTime ?? DateTime.Today.AddYears(1),
                            QuantityAvailable = CalculateInitialStockPieces(),
                            PurchasePrice = CurrentRate / CurrentTabletsPerBox,
                            MRP = CurrentPurchasePrice / CurrentTabletsPerBox
                        };
                        _repo.InsertStock(stock);
                    }
                }
                else
                {
                    m.ProductID = SelectedProduct!.ProductID;
                    var saved = _editingStockId.HasValue
                        ? _repo.UpdateProductAndStockBatch(m, _editingStockId.Value, CurrentRate, CurrentPurchasePrice)
                        : _repo.Update(m);
                    if (!saved)
                        throw new InvalidOperationException("The product was not found or has already been archived.");
                }
            });

            StatusMessage = isNewProduct ? "Product added." : "Product updated.";
            if (isNewProduct)
                LogActivity("Product Added", $"New product '{m.Name}' added to inventory", "Products");
            else
                LogActivity("Product Updated", $"Product '{m.Name}' was updated", "Products");
            
            Mode = FormMode.View;
            NotifyButtonStates();
            _editingStockId = null;
            await InitializeAsync();
            ProductSaved?.Invoke(m);
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
        CancelRequested?.Invoke();
    }

    [RelayCommand] private void Find() { ShowList = !ShowList; FilterProducts(); }
    [RelayCommand] private void QuickAddCompany() => RequestAddCompany?.Invoke();
    [RelayCommand]
    private async Task AddAnotherAsync()
    {
        if (Mode != FormMode.Add) return;
        await SaveAsync();
        if (Mode == FormMode.View) await NewAsync();
    }

    public async Task PreselectCompanyAsync(int companyId)
    {
        Companies = new ObservableCollection<Company>(await Task.Run(_companyRepo.GetAll));
        if (Mode == FormMode.View) NewCommand.Execute(null);
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == companyId);
        PreselectedEntityId = companyId;
    }
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

    partial void OnSearchTermChanged(string value)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        _ = FilterAfterTypingPauseAsync(_searchCancellation.Token);
    }

    private async Task FilterAfterTypingPauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken);
            if (!cancellationToken.IsCancellationRequested) FilterProducts();
        }
        catch (OperationCanceledException) { }
    }
    partial void OnCompanyFilterChanged(Company? value) => FilterProducts();
    [RelayCommand] private void ClearCompanyFilter() => CompanyFilter = null;

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

        if (ReturnType == "Supplier Return" && ReturnQuantity > ReturnTargetProduct.TotalStock)
        {
            StatusMessage = $"Cannot return more than available stock ({ReturnTargetProduct.TotalStock}).";
            return;
        }

        decimal unitPrice = ReturnType == "Patient Return"
                    ? ReturnTargetProduct.PricePerTablet
                    : ReturnTargetProduct.PricePerTablet;

        decimal refundAmount = unitPrice * ReturnQuantity;

        var ret = new ProductReturn
        {
            ReturnNo    = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
            ProductId   = ReturnTargetProduct.ProductID,
            BatchNo     = ReturnTargetProduct.PCode.ToString(),
            Quantity    = ReturnQuantity,
            StockQuantity = ReturnQuantity,
            UnitType = "Pieces",
            ReturnType  = ReturnType,
            Reason      = ReturnReason,
            RefundAmount = refundAmount,
            CreatedBy   = CurrentUser?.UserID,
            CreatedAt   = DateTime.Now,
            IsPosted = true
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
            2 => Products.Where(m => !m.IsExpired && m.TotalStock > 0),
            _ => Products
        };
        if (CompanyFilter != null) source = source.Where(m => m.CompanyID == CompanyFilter.CompanyID);

        IEnumerable<Product> result;
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            result = source;
        }
        else
        {
            var t = SearchTerm.ToLower();
            result = source.Where(m =>
                (m.Name?.ToLower().Contains(t) ?? false) ||
                (m.GenericName?.ToLower().Contains(t) ?? false) ||
                (m.CompanyName?.ToLower().Contains(t) ?? false) ||
                m.PCode.ToString().Contains(t) ||
                (m.CompanyID.HasValue && Companies.FirstOrDefault(c => c.CompanyID == m.CompanyID)?.CCode.ToString().Contains(t) == true));
        }

        FilteredProducts.Clear();
        foreach (var item in result)
            FilteredProducts.Add(item);

        FilterProductStockBatches();
    }

    private void FilterProductStockBatches()
    {
        IEnumerable<ProductStockBatchDto> result = ProductStockBatches;
        if (CompanyFilter != null)
            result = result.Where(batch => batch.CompanyID == CompanyFilter.CompanyID);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.ToLower();
            result = result.Where(batch =>
                batch.ProductName.ToLower().Contains(term) ||
                (batch.CompanyName?.ToLower().Contains(term) ?? false) ||
                batch.PCode.ToString().Contains(term));
        }

        FilteredProductStockBatches.Clear();
        foreach (var batch in result)
            FilteredProductStockBatches.Add(batch);
    }

    private void ClearFields()
    {
        Name = string.Empty;
        GenericName = string.Empty;
        Type = string.Empty;
        Category = string.Empty;
        Rack = string.Empty;
        ExpiryDate = null;
        Rate = "0.00";
        PurchasePrice = "0.00";
        TabletsPerBox = "1";
        InitialQuantityPacks = "0";
        MinimumStockLevel = "10";
        SelectedCompany = null;
        CompanyName = string.Empty;
        _editingStockId = null;
        NotifyCalculatedTotals();
    }

    private void FillFields(Product m)
    {
        PCode = m.PCode;
        Name = m.Name;
        GenericName = m.GenericName ?? string.Empty;
        Type = m.Type ?? string.Empty;
        Category = m.Category ?? string.Empty;
        Rack = m.Rack ?? string.Empty;
        ExpiryDate = m.EarliestExpiry.HasValue ? new DateTimeOffset(m.EarliestExpiry.Value, TimeSpan.Zero) : null;
        Rate = m.Rate.ToString("F2");
        PurchasePrice = m.MRP.ToString("F2");
        TabletsPerBox = Math.Max(1, m.TabletsPerBox).ToString();
        InitialQuantityPacks = m.PiecesPerUnit > 0 ? (m.TotalStock / m.PiecesPerUnit).ToString() : "0";
        MinimumStockLevel = m.MinimumStockLevel.ToString();
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == m.CompanyID);
        CompanyName = m.CompanyName ?? string.Empty;
        NotifyCalculatedTotals();
    }

    private Product BuildProduct() => new()
    {
        PCode = PCode,
        Name = Name,
        GenericName = string.IsNullOrWhiteSpace(GenericName) ? null : GenericName.Trim(),
        Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
        Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
        Rack = string.IsNullOrWhiteSpace(Rack) ? null : Rack.Trim(),

        Rate = decimal.TryParse(Rate, out var rate) ? rate : 0,
        Barcode = SelectedProduct?.Barcode,
        PurchasePrice = SelectedProduct?.PurchasePrice ?? 0,
        SellingPrice = decimal.TryParse(PurchasePrice, out var sp) ? sp : 0,
        TabletsPerBox = int.TryParse(TabletsPerBox, out var tpb) ? Math.Max(1, tpb) : 1,

        MinimumStockLevel = int.TryParse(MinimumStockLevel, out var ms) ? ms : 10,
        LastStockUpdateDate = SelectedProduct?.LastStockUpdateDate ?? DateTime.Today,
        // Editing must retain the existing company when the lookup is temporarily
        // unavailable; a company selection is mandatory only for a new product.
        CompanyID = SelectedCompany?.CompanyID ?? SelectedProduct?.CompanyID,
        CompanyName = SelectedCompany?.Name ?? SelectedProduct?.CompanyName ??
                      (string.IsNullOrWhiteSpace(CompanyName) ? null : CompanyName.Trim()),
        SupplierID = SelectedProduct?.SupplierID,
        SupplierName = SelectedProduct?.SupplierName,
        IsReturnable = SelectedProduct?.IsReturnable ?? true
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    partial void OnPurchasePriceChanged(string value) => NotifyCalculatedTotals();
    partial void OnRateChanged(string value) => NotifyCalculatedTotals();
    partial void OnTabletsPerBoxChanged(string value) => NotifyCalculatedTotals();
    partial void OnInitialQuantityPacksChanged(string value) => NotifyCalculatedTotals();
    partial void OnSelectedCompanyChanged(Company? value)
    {
        CompanyName=value?.Name ?? string.Empty;
        RefreshNextProductCode();
    }

    private void NotifyCalculatedTotals()
    {
        OnPropertyChanged(nameof(PricePerTabletDisplay));
        OnPropertyChanged(nameof(EstimatedLandedCostDisplay));
        OnPropertyChanged(nameof(EstimatedMarginDisplay));
        OnPropertyChanged(nameof(IsRatePreviewVisible));
        OnPropertyChanged(nameof(InitialStockPiecesDisplay));
    }

    private int CalculateInitialStockPieces()
    {
        var piecesPerUnit = int.TryParse(TabletsPerBox, out var tpb) ? Math.Max(1, tpb) : 1;
        var packs = int.TryParse(InitialQuantityPacks, out var qty) ? Math.Max(0, qty) : 0;
        return packs * piecesPerUnit;
    }

    private void RefreshNextProductCode()
    {
        if (Mode != FormMode.Add) return;

        PCode = SelectedCompany?.CompanyID is int companyId
            ? _repo.GetNextPCode(companyId)
            : _repo.GetNextPCode();
    }

    private static string FormatMoney(decimal value) => $"Rs. {value:N2}";

    public async Task InitializeAsync()
    {
        try
        {
            var meds = await Task.Run(() => _repo.GetAll());
            var batches = await Task.Run(() => _repo.GetProductInventory());
            var totalStockValue = await Task.Run(() => _repo.GetTotalStockValue());
            var comps = await Task.Run(() => _companyRepo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var prevSelectedId = SelectedProduct?.ProductID;
                StatusMessage = string.Empty;
                Companies = new ObservableCollection<Company>(comps);
                Products = new ObservableCollection<Product>(meds);
                ProductStockBatches = new ObservableCollection<ProductStockBatchDto>(batches);
                FilterProducts();

                if (prevSelectedId.HasValue)
                {
                    SelectedProduct = FilteredProducts.FirstOrDefault(p => p.ProductID == prevSelectedId.Value);
                }

                LowStockCount = Products.GroupBy(m => m.ProductID)
                    .Count(g => g.First().AggregateStock <= g.First().MinimumStockPieces && !g.Any(m => m.IsExpired));
                ExpiredCount = Products.Count(m => m.IsExpired);
                // Use each batch's T.P. rather than the product's most recent price.
                TotalInventoryValue = FormatMoney(totalStockValue);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load products: {ex.Message}");
        }
    }
}

