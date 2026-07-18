using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class SaleViewModel : ViewModelBase
{
    private readonly SaleRepository _repo;
    private readonly PatientRepository _patientRepo;
    private readonly ProductRepository _productRepo;

    public SaleViewModel(SaleRepository repo, PatientRepository patientRepo, ProductRepository productRepo)
    {
        _repo = repo;
        _patientRepo = patientRepo;
        _productRepo = productRepo;
    }

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showForm;
    
    [ObservableProperty] private ObservableCollection<Sale> _sales = new();
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new(); // non-expired
    [ObservableProperty] private Sale? _selectedSale;

    // KPI summary counts
    [ObservableProperty] private int _totalInvoicesCount;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _revenueToday;
    [ObservableProperty] private decimal _avgSaleValue;

    // Header Fields
    [ObservableProperty] private string _invoiceNumber = string.Empty;
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private string _walkInPatientName = string.Empty;
    [ObservableProperty] private DateTimeOffset _saleDate = DateTimeOffset.Now;
    [ObservableProperty] private decimal _consultationFee;
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

    public bool PatientIsSelected => SelectedPatient != null;
    public bool IsWalkIn => SelectedPatient == null;

    public decimal GrandTotal => ConsultationFee + LineItems.Sum(x => x.Quantity * x.ProductPrice - x.Discount + x.Tax);

    public bool MutationEnabled => !ShowForm;
    public bool SaveCancelEnabled => ShowForm && Mode == FormMode.Add;
    public bool IsNewSale => Mode == FormMode.Add;

    [RelayCommand]
    private async Task NewAsync()
    {
        ClearFields();
        // Generate SAL-YYYYMMDD-XXX invoice number
        var today = DateTime.Today;
        var count = await Task.Run(() => _repo.GetCountForDate(today));
        InvoiceNumber = $"SAL-{today:yyyyMMdd}-{(count + 1):D3}";
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
            SelectedPatient = Patients.FirstOrDefault(p => p.PatientID == SelectedSale.PatientID);
            SaleDate = new DateTimeOffset(SelectedSale.SaleDate);
            ConsultationFee = SelectedSale.ConsultationFee;
            PaymentMethod = SelectedSale.PaymentMethod ?? "Cash";
            
            var saleWithItems = await Task.Run(() => _repo.GetByIdWithItems(SelectedSale.SaleID));
            var items = saleWithItems?.Items ?? new List<SaleItem>();
            LineItems = new ObservableCollection<SaleItem>(items);
            
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
    private void AddLineItem()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a product."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }
        if (Quantity > SelectedProduct.Stock) { StatusMessage = $"Only {SelectedProduct.Stock} in stock."; return; }

        var item = new SaleItem
        {
            ProductID = SelectedProduct.ProductID,
            ProductName = SelectedProduct.Name,
            Quantity = Quantity,
            Discount = Discount,
            Tax = Tax,
            ProductPrice = ProductPrice
        };

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        
        // Reset inputs
        SelectedProduct = null;
        Quantity = 1;
        Discount = 0;
        Tax = 0;
        ProductPrice = 0;
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
        // Patient is optional — allow walk-in
        string patientNameForSale = SelectedPatient?.Name ?? 
            (string.IsNullOrWhiteSpace(WalkInPatientName) ? "Walk-in" : WalkInPatientName.Trim());

        var s = new Sale
        {
            InvoiceNumber = InvoiceNumber,
            SaleDate = SaleDate.DateTime,
            PatientID = SelectedPatient?.PatientID,
            PatientName = patientNameForSale,
            ConsultationFee = ConsultationFee,
            GrandTotal = GrandTotal,
            PaymentMethod = PaymentMethod,
            IsPosted = true,
            Items = LineItems.ToList()
        };

        try
        {
            if (Mode == FormMode.Add)
            {
                await Task.Run(() => _repo.Insert(s));
                StatusMessage = "Sale posted successfully. Stock updated.";
                LogActivity("Sale Completed", $"Invoice #{s.InvoiceNumber} posted for {s.PatientName} — Rs. {s.GrandTotal:N2}", "Sales");
                WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            }
            
            Mode = FormMode.View;
            ShowForm = false;
            NotifyButtonStates();
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving sale: {ex.Message}";
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
            var patients = await Task.Run(() => _patientRepo.GetAll());
            var products = await Task.Run(() => _productRepo.GetAll());
            var sales = await Task.Run(() => _repo.GetAll());
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                Patients = new ObservableCollection<Patient>(patients);
                Products = new ObservableCollection<Product>(
                    products.Where(m => !m.IsExpired && m.Stock > 0).OrderBy(m => m.Name));
                FilteredProducts = new ObservableCollection<Product>(Products);
                Sales = new ObservableCollection<Sale>(sales.OrderByDescending(s => s.SaleDate));

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
        SelectedPatient = null;
        WalkInPatientName = string.Empty;
        SaleDate = DateTimeOffset.Now;
        ConsultationFee = 0;
        PaymentMethod = "Cash";
        LineItems.Clear();
        ProductSearchTerm = string.Empty;
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsNewSale));
        OnPropertyChanged(nameof(PatientIsSelected));
        OnPropertyChanged(nameof(IsWalkIn));
    }

    partial void OnSelectedPatientChanged(Patient? value)
    {
        OnPropertyChanged(nameof(PatientIsSelected));
        OnPropertyChanged(nameof(IsWalkIn));
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
            ProductPrice = value.SellingPrice;
        }
    }
    
    partial void OnConsultationFeeChanged(decimal value)
    {
        OnPropertyChanged(nameof(GrandTotal));
    }
}
