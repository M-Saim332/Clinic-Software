using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.Services;

namespace ClinicSystem.UI.ViewModels.Patients;


public partial class PatientRegistryViewModel : ViewModelBase, ISearchable
{
    private readonly PatientRepository _repo;
    private readonly SaleRepository _saleRepo;

    private readonly ProductRepository _productRepo;
    private readonly PrescriptionRepository _prescriptionRepo;

    public PatientRegistryViewModel(
        PatientRepository repo,
        SaleRepository saleRepo,
        ProductRepository productRepo,
        PrescriptionRepository prescriptionRepo)
    {
        _repo = repo;
        _saleRepo = saleRepo;
        _productRepo = productRepo;
        _prescriptionRepo = prescriptionRepo;

        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (s, e) => 
        {
            if (SelectedTab == 0) _ = InitializeAsync(); 
        };
        timer.Start();
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
    public string SearchPlaceholder => "Search Patients...";

    [ObservableProperty] private int _totalPatientsCount;
    [ObservableProperty] private int _activeThisMonthCount;
    [ObservableProperty] private int _waitingTodayCount;

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
    public bool IsAdmin             => CurrentUser?.IsAdmin ?? false;
    [ObservableProperty] private bool _showDeleteAllConfirm;

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
    [ObservableProperty] private string _cNIC = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _reasonOfVisit = string.Empty;
    [ObservableProperty] private TimeSpan? _nextAppointmentTime;
    [ObservableProperty] private ObservableCollection<Product> _availableDrugs = new();
    [ObservableProperty] private ObservableCollection<Product> _filteredDrugs = new();
    [ObservableProperty] private ObservableCollection<PrescriptionItemRow> _selectedDrugs = new();
    [ObservableProperty] private ObservableCollection<Prescription> _visitHistory = new();
    [ObservableProperty] private Product? _selectedDrug;
    [ObservableProperty] private string _drugSearch = string.Empty;
    [ObservableProperty] private int _drugQuantity = 1;
    [ObservableProperty] private string _dosage = string.Empty;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };



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
    private async Task ViewSpecificAsync(Patient p)
    {
        if (p == null) return;
        SelectedPatient = p;
        FillFields(p);
        Mode = FormMode.Details;
        NotifyButtonStates();
        StatusMessage = "Viewing patient details.";
        await LoadClinicalDetailAsync(p.PatientID);
    }

    [RelayCommand]
    private void AddDrug()
    {
        if (SelectedDrug == null) { StatusMessage = "Select an in-stock drug."; return; }
        if (DrugQuantity <= 0) { StatusMessage = "Drug quantity must be greater than zero."; return; }
        SelectedDrugs.Add(new PrescriptionItemRow { ProductID=SelectedDrug.ProductID, ProductName=SelectedDrug.Name,
            Quantity=DrugQuantity, Dosage=Dosage, AvailableStock=SelectedDrug.Stock });
        SelectedDrug=null; DrugQuantity=1; Dosage=string.Empty;
    }

    [RelayCommand] private void RemoveDrug(PrescriptionItemRow? row) { if (row != null) SelectedDrugs.Remove(row); }

    [RelayCommand]
    private async Task PostAndSyncAsync()
    {
        if (SelectedPatient == null) { StatusMessage = "Select a clinical patient."; return; }
        if (SelectedDrugs.Count == 0) { StatusMessage = "Select at least one drug."; return; }
        var prescription = new Prescription {
            PatientID=SelectedPatient.PatientID, DoctorID=CurrentUser?.UserID ?? 0, VisitDate=DateTime.Now,
            Diagnosis=null, Notes=ReasonOfVisit,
            Items=SelectedDrugs.Select(d => new PrescriptionItem { ProductID=d.ProductID, Quantity=d.Quantity, Dosage=d.Dosage }).ToList()
        };
        try
        {
            await Task.Run(() => _prescriptionRepo.Insert(prescription, "SentToPharmacy"));
            await Task.Run(() => _repo.ShareWithPharma(SelectedPatient.PatientID));
            await Task.Run(() => _repo.UpdateVisitStatus(SelectedPatient.PatientID, "Visited", DateTime.Today));
            SelectedDrugs.Clear();
            await LoadClinicalDetailAsync(SelectedPatient.PatientID);
            StatusMessage = "Patient sent to the pharmacist. Reception has also been notified.";
            LogActivity("Patient Sent to Pharmacist", $"Prescription for {SelectedPatient.Name} is ready for pharmacy review", "Prescriptions");
        }
        catch (Exception ex) { StatusMessage = $"Unable to post visit: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task PrintClinicalSummaryAsync()
    {
        if (SelectedPatient == null) return;
        var items = SelectedDrugs.Select(d => new PrescriptionItem
        {
            ProductID = d.ProductID, ProductName = d.ProductName, Quantity = d.Quantity, Dosage = d.Dosage
        }).ToList();
        if (items.Count == 0) items = VisitHistory.FirstOrDefault()?.Items.ToList() ?? new List<PrescriptionItem>();
        if (items.Count == 0) { StatusMessage = "Add medicines before printing the prescription."; return; }
        var prescription = new Prescription
        {
            PatientID = SelectedPatient.PatientID, PatientName = SelectedPatient.Name, PatientAge = SelectedPatient.Age,
            PatientGender = SelectedPatient.Gender, PatientPhone = SelectedPatient.Phone, DoctorID = CurrentUser?.UserID ?? 0,
            DoctorName = CurrentUser?.FullName, VisitDate = DateTime.Now, Notes = ReasonOfVisit, Items = items
        };
        if (await PrescriptionPrintService.ExportAsync(prescription)) StatusMessage = "Patient prescription is ready to print.";
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
    private void RequestDeleteAll()
    {
        if (!IsAdmin) { StatusMessage = "Only an administrator can archive all patients."; return; }
        ShowDeleteAllConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAllAsync()
    {
        ShowDeleteAllConfirm = false;
        if (!IsAdmin) { StatusMessage = "Administrator authorization is required."; return; }
        var count = await Task.Run(_repo.SoftDeleteAll);
        StatusMessage = $"{count} patient(s) archived. Historical records were preserved.";
        LogActivity("Patients Archived", $"Archived all {count} active patients", "Patients");
        await InitializeAsync();
    }

    [RelayCommand]
    private void CancelDeleteAll() => ShowDeleteAllConfirm = false;

    [RelayCommand]
    private void BookAppointmentSpecific(Patient p)
    {
        if (p == null) return;
        RequestBookAppointment?.Invoke(p);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ClinicSystem.UI.Helpers.ValidationHelper.IsValidName(Name)) { StatusMessage = "Valid Name is required (min 2 chars, no numbers)."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.IsValidPhone(Phone)) { StatusMessage = "Phone number must contain exactly 11 digits."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateCNIC(CNIC, required: false)) { StatusMessage = "CNIC must contain exactly 13 digits."; return; }
        
        int age = 0;
        if (!string.IsNullOrWhiteSpace(Age) && (!int.TryParse(Age, out age) || age < 0)) { StatusMessage = "Age must be a positive number."; return; }

        var p = BuildPatient();

        try
        {
            await Task.Run(() =>
            {
                if (Mode == FormMode.Add)
                {
                    p.VisitStatus = "Waiting";
                    p.LastVisitDate = DateTime.Today;
                    _repo.Insert(p);
                }
                else
                {
                    p.PatientID = SelectedPatient!.PatientID;
                    p.VisitStatus = SelectedPatient.VisitStatus;
                    p.LastVisitDate = SelectedPatient.LastVisitDate;
                    _repo.Update(p);
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save patient: {ex.Message}";
            return;
        }

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
    private async Task MarkAsCancelledAsync(Patient p)
    {
        if (p == null) return;
        await Task.Run(() => _repo.UpdateVisitStatus(p.PatientID, "Cancelled", DateTime.Today));
        StatusMessage = $"Patient '{p.Name}' visit cancelled.";
        _ = InitializeAsync();
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
    partial void OnDrugSearchChanged(string value) => FilterDrugList();

    // ── Helpers ────────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll("Clinical"));
            var drugs = await Task.Run(() => _productRepo.GetPrescribable());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = string.Empty;
                Patients = new ObservableCollection<Patient>(list);
                AvailableDrugs = new ObservableCollection<Product>(drugs);
                FilterDrugList();
                
                var now = DateTime.Now.TimeOfDay;
                WaitingPatientsList = new ObservableCollection<Patient>(
                    list.Where(p => 
                    {
                        if (p.VisitStatus != "Waiting" || p.LastVisitDate?.Date != DateTime.Today) return false;
                        if (p.NextAppointmentTime.HasValue && (now - p.NextAppointmentTime.Value).TotalMinutes > 30) return false;
                        return true;
                    }));
                VisitedPatientsList = new ObservableCollection<Patient>(
                    list.Where(p => p.VisitStatus == "Visited" && p.LastVisitDate?.Date == DateTime.Today));

                FilterPatients();

                TotalPatientsCount = Patients.Count;
                ActiveThisMonthCount = Patients.Count(p => p.LastVisitDate >= DateTime.Today.AddMonths(-1));
                WaitingTodayCount = WaitingPatientsList.Count;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load patients: {ex.Message}");
        }
    }

    private async Task LoadClinicalDetailAsync(int patientId)
    {
        var history = await Task.Run(() => _prescriptionRepo.GetByPatient(patientId)
            .Select(p => _prescriptionRepo.GetByIdWithItems(p.PrescriptionID) ?? p).ToList());
        VisitHistory = new ObservableCollection<Prescription>(history);
    }

    private void FilterDrugList()
    {
        var q=DrugSearch.Trim();
        FilteredDrugs = new ObservableCollection<Product>(string.IsNullOrWhiteSpace(q) ? AvailableDrugs : AvailableDrugs.Where(d =>
            d.Name.Contains(q,StringComparison.OrdinalIgnoreCase) || (d.GenericName?.Contains(q,StringComparison.OrdinalIgnoreCase) ?? false)));
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
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            FilteredPatients = new ObservableCollection<Patient>(
                sourceList.Where(p => p.Name.ToLower().Contains(term)
                                   || p.PatientID.ToString().Contains(term)
                                   || (p.Phone?.ToLower().Replace(" ", "").Replace("-", "").Contains(term) ?? false)
                                   || (p.CNIC?.ToLower().Replace(" ", "").Replace("-", "").Contains(term) ?? false)
                                   || (p.Address?.ToLower().Contains(term) ?? false)
                                   || (p.ReasonOfVisit?.ToLower().Contains(term) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Age = string.Empty; Gender = "Male";
        Phone = string.Empty; CNIC = string.Empty; Address = string.Empty;
        ReasonOfVisit = string.Empty;
        NextAppointmentTime = null;
    }

    private void FillFields(Patient p)
    {
        Name = p.Name; Age = p.Age?.ToString() ?? string.Empty;
        Gender = p.Gender ?? "Male"; Phone = p.Phone ?? string.Empty;
        CNIC = p.CNIC ?? string.Empty;
        Address = p.Address ?? string.Empty;
        ReasonOfVisit = p.ReasonOfVisit ?? string.Empty;
        NextAppointmentTime = p.NextAppointmentTime;
    }

    private Patient BuildPatient() => new()
    {
        Name = Name, Age = int.TryParse(Age, out var a) ? a : null,
        Gender = Gender, Phone = Phone, CNIC = CNIC, Address = Address,
        ReasonOfVisit = ReasonOfVisit, PatientContext = "Clinical",
        NextAppointmentTime = NextAppointmentTime
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
