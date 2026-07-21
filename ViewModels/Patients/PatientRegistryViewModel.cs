using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Patients;


public partial class PatientRegistryViewModel : ViewModelBase
{
    private readonly PatientRepository _repo;
    private readonly SaleRepository _saleRepo;
    private readonly ReturnRepository _returnRepo;
    private readonly ProductRepository _productRepo;

    public PatientRegistryViewModel(
        PatientRepository repo,
        SaleRepository saleRepo,
        ReturnRepository returnRepo,
        ProductRepository productRepo)
    {
        _repo = repo;
        _saleRepo = saleRepo;
        _returnRepo = returnRepo;
        _productRepo = productRepo;
    }

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    [NotifyPropertyChangedFor(nameof(IsListViewVisible))]
    [NotifyPropertyChangedFor(nameof(PkEditable))]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(ShowSaveButton))]
    [NotifyPropertyChangedFor(nameof(ShowEditButton))]
    private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    [ObservableProperty] private int _totalPatientsCount;
    [ObservableProperty] private int _activeThisMonthCount;
    [ObservableProperty] private int _waitingTodayCount;
    [ObservableProperty] private string _avgConsultationFee = "Rs. 0.00";

    // ── Tab State ──────────────────────────────────────────────────────────
    [ObservableProperty] private int _selectedTab = 0; // 0 = Waiting, 1 = Visited, 2 = All
    partial void OnSelectedTabChanged(int value) => FilterPatients();


    // ── Button visibility ──────────────────────────────────────────────────
    public bool MutationEnabled     => Mode == FormMode.View;
    public bool SaveCancelEnabled   => Mode != FormMode.View;
    public bool IsListViewVisible   => Mode == FormMode.View;   // explicit — avoids compiled-binding negation issue
    public bool PkEditable          => Mode == FormMode.Add;
    public bool IsReadOnly          => Mode == FormMode.Details || Mode == FormMode.View;
    public bool ShowSaveButton      => Mode == FormMode.Add || Mode == FormMode.Edit;
    public bool ShowEditButton      => Mode == FormMode.Details;

    // Navigation delegates
    public Action<Patient>? RequestBookAppointment { get; set; }

    // ── Data ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Patient> _waitingPatientsList = new();
    [ObservableProperty] private ObservableCollection<Patient> _visitedPatientsList = new();
    
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();
    [ObservableProperty] private Patient? _selectedPatient;

    // Edit fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private string _gender = "Male";
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _prescription = string.Empty;
    [ObservableProperty] private string _consultationFee = "0.00";
    [ObservableProperty] private string _discount = "0.00";

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };

    // ── Patient Return State ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isReturnModalOpen;
    [ObservableProperty] private ObservableCollection<SaleItem> _returnableItems = new();
    [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
    
    [ObservableProperty] private SaleItem? _selectedReturnItem;
    [ObservableProperty] private Product? _selectedReturnProduct;
    
    [ObservableProperty] private int _returnQuantity = 1;
    [ObservableProperty] private string _returnReason = "Patient Changed Mind";
    public List<string> ReturnReasons { get; } = new() { "Expired", "Damaged", "Wrong Item", "Patient Changed Mind", "Other" };

    // ── Commands ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new patient details and click Save.";
    }

    [RelayCommand]
    private void EditSpecific(Patient p)
    {
        if (p == null) return;
        SelectedPatient = p;
        FillFields(p);
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Edit patient details and click Save.";
    }

    [RelayCommand]
    private void ViewSpecific(Patient p)
    {
        if (p == null) return;
        SelectedPatient = p;
        FillFields(p);
        Mode = FormMode.Details;
        NotifyButtonStates();
        StatusMessage = "Viewing patient details.";
    }

    [RelayCommand]
    private async Task DeleteSpecificAsync(Patient p)
    {
        if (p == null) return;
        var ok = await Task.Run(() => _repo.Delete(p.PatientID));
        if (ok)
        {
            StatusMessage = "Patient deleted.";
            LogActivity("Patient Deleted", $"Patient '{p.Name}' record deleted", "Patients");
            _ = InitializeAsync();
            if (SelectedPatient?.PatientID == p.PatientID) SelectedPatient = null;
        }
        else StatusMessage = "Cannot delete — patient has existing prescriptions.";
    }

    [RelayCommand]
    private void BookAppointmentSpecific(Patient p)
    {
        if (p == null) return;
        RequestBookAppointment?.Invoke(p);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name is required."; return; }

        var p = BuildPatient();
        await Task.Run(() =>
        {
            if (Mode == FormMode.Add) _repo.Insert(p);
            else { p.PatientID = SelectedPatient!.PatientID; _repo.Update(p); }
        });

        StatusMessage = Mode == FormMode.Add ? "Patient added." : "Patient updated.";
        if (Mode == FormMode.Add)
            LogActivity("Patient Registered", $"New patient '{p.Name}' registered", "Patients");
        else
            LogActivity("Patient Updated", $"Patient '{p.Name}' profile updated", "Patients");
        Mode = FormMode.View;
        NotifyButtonStates();
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task MarkAsWaitingAsync(Patient p)
    {
        if (p == null) return;
        await Task.Run(() => _repo.UpdateVisitStatus(p.PatientID, "Waiting", DateTime.Today));
        StatusMessage = $"Patient '{p.Name}' marked as waiting.";
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task MarkAsVisitedAsync(Patient p)
    {
        if (p == null) return;
        await Task.Run(() => _repo.UpdateVisitStatus(p.PatientID, "Visited", DateTime.Today));
        StatusMessage = $"Patient '{p.Name}' marked as visited today.";
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task OpenReturnModalAsync(Patient p)
    {
        if (p == null) return;
        SelectedPatient = p;
        
        var sales = await Task.Run(() => _saleRepo.GetByPatientIdWithItems(p.PatientID));
        var pastItems = sales.SelectMany(s => s.Items).ToList();
        
        var products = await Task.Run(() => _productRepo.GetAll());

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ReturnableItems = new ObservableCollection<SaleItem>(pastItems);
            AllProducts = new ObservableCollection<Product>(products);
            SelectedReturnItem = ReturnableItems.FirstOrDefault();
            SelectedReturnProduct = null;
            ReturnQuantity = 1;
            ReturnReason = "Patient Changed Mind";
            IsReturnModalOpen = true;
        });
    }

    [RelayCommand]
    private void CloseReturnModal() => IsReturnModalOpen = false;

    [RelayCommand]
    private async Task SubmitReturnAsync()
    {
        if (SelectedPatient == null) return;
        
        int? productId = SelectedReturnItem?.ProductID ?? SelectedReturnProduct?.ProductID;
        decimal? unitPrice = SelectedReturnItem?.ProductPrice ?? SelectedReturnProduct?.SellingPrice;
        int? saleId = SelectedReturnItem?.SaleID;

        if (productId == null || productId == 0)
        {
            StatusMessage = "Please select an item to return.";
            return;
        }

        if (ReturnQuantity <= 0)
        {
            StatusMessage = "Return quantity must be > 0.";
            return;
        }

        // SelectedReturnItem max quantity validation
        if (SelectedReturnItem != null && ReturnQuantity > SelectedReturnItem.Quantity)
        {
            StatusMessage = $"Cannot return more than purchased ({SelectedReturnItem.Quantity}).";
            return;
        }

        decimal refundAmount = (unitPrice ?? 0) * ReturnQuantity;

        var ret = new ProductReturn
        {
            ReturnNo = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
            ProductId = productId.Value,
            PatientId = SelectedPatient.PatientID,
            SaleId = saleId,
            Quantity = ReturnQuantity,
            ReturnType = "Patient Return",
            Reason = ReturnReason,
            RefundAmount = refundAmount,
            CreatedBy = CurrentUser?.UserID,
            CreatedAt = DateTime.Now
        };

        try
        {
            await Task.Run(() => _returnRepo.Insert(ret));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = $"Successfully returned {ReturnQuantity} item(s). Refund: Rs. {refundAmount:N2}";
                LogActivity("Patient Return", $"Returned {ReturnQuantity} units for patient {SelectedPatient.Name}", "Returns");
                IsReturnModalOpen = false;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "Failed to process return: " + ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        if (SelectedPatient != null) FillFields(SelectedPatient);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Find()
    {
        ShowList = !ShowList;
        FilterPatients();
    }

    [RelayCommand]
    private void List() { ShowList = true; FilterPatients(); }

    [RelayCommand]
    private void CloseList() => ShowList = false;

    [RelayCommand]
    private void SelectFromList(Patient? p)
    {
        if (p == null) return;
        SelectedPatient = p;
        FillFields(p);
        ShowList = false;
    }

    partial void OnSearchTermChanged(string value) => FilterPatients();

    // ── Helpers ────────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = string.Empty;
                Patients = new ObservableCollection<Patient>(list);
                
                WaitingPatientsList = new ObservableCollection<Patient>(
                    list.Where(p => p.VisitStatus == "Waiting" && p.LastVisitDate?.Date == DateTime.Today));
                VisitedPatientsList = new ObservableCollection<Patient>(
                    list.Where(p => p.VisitStatus == "Visited" && p.LastVisitDate?.Date == DateTime.Today));

                FilterPatients();

                TotalPatientsCount = Patients.Count;
                ActiveThisMonthCount = Patients.Count(p => p.ConsultationFee > 0);
                WaitingTodayCount = WaitingPatientsList.Count;
                AvgConsultationFee = Patients.Count > 0
                    ? $"Rs. {Patients.Average(p => p.ConsultationFee):N2}"
                    : "Rs. 0.00";
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load patients: {ex.Message}");
        }
    }


    private void FilterPatients()
    {
        IEnumerable<Patient> sourceList = SelectedTab switch
        {
            0 => WaitingPatientsList,
            1 => VisitedPatientsList,
            _ => Patients
        };

        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredPatients = new ObservableCollection<Patient>(sourceList);
        else
        {
            var term = SearchTerm.ToLower();
            FilteredPatients = new ObservableCollection<Patient>(
                sourceList.Where(p => p.Name.ToLower().Contains(term)
                                   || (p.Phone?.ToLower().Contains(term) ?? false)
                                   || (p.Address?.ToLower().Contains(term) ?? false)
                                   || (p.Diagnosis?.ToLower().Contains(term) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Age = string.Empty; Gender = "Male";
        Phone = string.Empty; Address = string.Empty;
        Diagnosis = string.Empty; Prescription = string.Empty;
        ConsultationFee = "0.00"; Discount = "0.00";
    }

    private void FillFields(Patient p)
    {
        Name = p.Name; Age = p.Age?.ToString() ?? string.Empty;
        Gender = p.Gender ?? "Male"; Phone = p.Phone ?? string.Empty;
        Address = p.Address ?? string.Empty;
        Diagnosis = p.Diagnosis ?? string.Empty; Prescription = p.Prescription ?? string.Empty;
        ConsultationFee = p.ConsultationFee.ToString("F2"); Discount = p.Discount.ToString("F2");
    }

    private Patient BuildPatient() => new()
    {
        Name = Name, Age = int.TryParse(Age, out var a) ? a : null,
        Gender = Gender, Phone = Phone, Address = Address,
        Diagnosis = Diagnosis, Prescription = Prescription,
        ConsultationFee = decimal.TryParse(ConsultationFee, out var f) ? f : 0,
        Discount = decimal.TryParse(Discount, out var d) ? d : 0
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsListViewVisible));
        OnPropertyChanged(nameof(PkEditable));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(ShowEditButton));
    }
}
