using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Sales;

public partial class SaleViewModel : ViewModelBase
{
    private readonly SaleRepository _repo;
    private readonly PatientRepository _patientRepo;
    private readonly MedicineRepository _medicineRepo;

    public SaleViewModel(SaleRepository repo, PatientRepository patientRepo, MedicineRepository medicineRepo)
    {
        _repo = repo;
        _patientRepo = patientRepo;
        _medicineRepo = medicineRepo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<Sale> _sales = new();
    [ObservableProperty]
    private ObservableCollection<Patient> _patients = new();
    [ObservableProperty]
    private ObservableCollection<Medicine> _medicines = new(); // non-expired
    
    [ObservableProperty]
    private Sale? _selectedSale;

    // Header Fields
    [ObservableProperty]
    private string _invoiceNumber = string.Empty;
    [ObservableProperty]
    private Patient? _selectedPatient;
    [ObservableProperty]
    private DateTimeOffset _saleDate = DateTimeOffset.Now;
    [ObservableProperty]
    private decimal _consultationFee;
    [ObservableProperty]
    private string _paymentMethod = "Cash";

    public List<string> PaymentMethods { get; } = new() { "Cash", "Card", "Online" };

    // Line Items
    [ObservableProperty]
    private ObservableCollection<SaleItem> _lineItems = new();
    
    // Line Item Input
    [ObservableProperty]
    private Medicine? _selectedMedicine;
    [ObservableProperty]
    private int _quantity = 1;
    [ObservableProperty]
    private decimal _discount;
    [ObservableProperty]
    private decimal _tax;
    [ObservableProperty]
    private decimal _medicinePrice;

    public decimal GrandTotal => ConsultationFee + LineItems.Sum(x => x.Quantity * x.MedicinePrice - x.Discount + x.Tax);

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool IsNewSale => Mode == FormMode.Add;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Create new sale invoice.";
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedSale == null) { StatusMessage = "Select a sale first."; return; }
        
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
        NotifyButtonStates();
        StatusMessage = $"Viewing details for {InvoiceNumber}";
    }

    [RelayCommand]
    private void AddLineItem()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be > 0."; return; }
        if (Quantity > SelectedMedicine.Stock) { StatusMessage = $"Only {SelectedMedicine.Stock} in stock."; return; }

        var item = new SaleItem
        {
            MedicineID = SelectedMedicine.MedicineID,
            MedicineName = SelectedMedicine.Name,
            Quantity = Quantity,
            Discount = Discount,
            Tax = Tax,
            MedicinePrice = MedicinePrice
        };

        LineItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
        
        // Reset inputs
        SelectedMedicine = null;
        Quantity = 1;
        Discount = 0;
        Tax = 0;
        MedicinePrice = 0;
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
        if (SelectedPatient == null)
        {
            StatusMessage = "Patient is required.";
            return;
        }

        var s = new Sale
        {
            InvoiceNumber = InvoiceNumber,
            SaleDate = SaleDate.DateTime,
            PatientID = SelectedPatient.PatientID,
            ConsultationFee = ConsultationFee,
            GrandTotal = GrandTotal,
            PaymentMethod = PaymentMethod,
            IsPosted = true,
            Items = LineItems.ToList()
        };

        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(s));
            StatusMessage = "Sale posted successfully. Stock updated.";
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
        var patients = await Task.Run(() => _patientRepo.GetAll());
        var medicines = await Task.Run(() => _medicineRepo.GetAll());
        var sales = await Task.Run(() => _repo.GetAll());
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            Patients = new ObservableCollection<Patient>(patients);
            // Only show medicines that are not expired and have stock
            Medicines = new ObservableCollection<Medicine>(
                medicines.Where(m => !m.IsExpired && m.Stock > 0).OrderBy(m => m.Name));
            Sales = new ObservableCollection<Sale>(sales.OrderByDescending(s => s.SaleDate));
        });
    }

    private void ClearFields()
    {
        InvoiceNumber = string.Empty;
        SelectedPatient = null;
        SaleDate = DateTimeOffset.Now;
        ConsultationFee = 0;
        PaymentMethod = "Cash";
        LineItems.Clear();
        OnPropertyChanged(nameof(GrandTotal));
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsNewSale));
    }

    // Auto-fill price when medicine selected
    partial void OnSelectedMedicineChanged(Medicine? value)
    {
        if (value != null)
        {
            MedicinePrice = value.SellingPrice;
        }
    }
    
    // Update grand total when fee changes
    partial void OnConsultationFeeChanged(decimal value)
    {
        OnPropertyChanged(nameof(GrandTotal));
    }
}
