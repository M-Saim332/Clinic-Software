using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.Messages;
using ClinicSystem.UI.Services;
using ClinicSystem.UI.ViewModels.Prescriptions;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Patients;


public partial class PatientRegistryViewModel : ViewModelBase
{
    private readonly PatientRepository _repo;
    private readonly ProductRepository _productRepo;
    private readonly PrescriptionRepository _prescriptionRepo;

    public PatientRegistryViewModel(PatientRepository repo, ProductRepository productRepo, PrescriptionRepository prescriptionRepo)
    {
        _repo = repo;
        _productRepo = productRepo;
        _prescriptionRepo = prescriptionRepo;
    }

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    [ObservableProperty] private int _totalPatientsCount;
    [ObservableProperty] private int _activeThisMonthCount;
    [ObservableProperty] private int _waitingTodayCount;
    [ObservableProperty] private string _avgConsultationFee = "Rs. 0.00";


    // ── Button visibility ──────────────────────────────────────────────────
    public bool MutationEnabled     => Mode == FormMode.View;
    public bool SaveCancelEnabled   => Mode != FormMode.View;
    public bool IsListViewVisible   => Mode == FormMode.View;   // explicit — avoids compiled-binding negation issue
    public bool PkEditable          => Mode == FormMode.Add;

    // ── Data ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();
    [ObservableProperty] private Patient? _selectedPatient;

    // Edit fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private string _gender = "Male";
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    
    private string _cnic = string.Empty;
    public string CNIC
    {
        get => _cnic;
        set => SetProperty(ref _cnic, value);
    }
    
    [ObservableProperty] private string _reasonOfVisit = string.Empty;
    [ObservableProperty] private TimeSpan? _nextAppointmentTime;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _prescription = string.Empty;
    [ObservableProperty] private string _consultationFee = "0.00";
    [ObservableProperty] private string _discount = "0.00";
    [ObservableProperty] private DateTimeOffset _appointmentDate = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan _appointmentTime = DateTime.Now.TimeOfDay;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSavePatient))]
    [NotifyPropertyChangedFor(nameof(CanUsePrescriptionActions))]
    private bool _isReadOnly;
    [ObservableProperty] private int _selectedTab;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };

    public bool IsListEmpty => FilteredPatients.Count == 0;
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    // ── View-state booleans required by the view ──────────────────────────
    public bool ShowEditButton  => Mode == FormMode.Edit || Mode == FormMode.View;
    public bool ShowSaveButton  => Mode == FormMode.Add  || Mode == FormMode.Edit;
    public bool CanSavePatient => ShowSaveButton && !IsReadOnly;
    public bool CanUsePrescriptionActions => Mode == FormMode.Edit && SelectedPatient != null;

    [ObservableProperty] private bool _showDeleteAllConfirm;

    // ── Drug-selection (prescription inline) ──────────────────────────────
    [ObservableProperty] private string _drugSearch  = string.Empty;
    [ObservableProperty] private string _dosage      = string.Empty;
    [ObservableProperty] private int    _drugQuantity = 1;
    [ObservableProperty] private Product? _selectedDrug;
    [ObservableProperty] private ObservableCollection<Product>       _availableDrugs = new();
    [ObservableProperty] private ObservableCollection<Product>       _filteredDrugs = new();
    [ObservableProperty] private ObservableCollection<PrescriptionItemRow> _selectedDrugs = new();

    // ── Visit history ─────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Prescription> _visitHistory = new();

    /// <summary>Fired when the user clicks "Book Appointment" for a patient.</summary>
    public event Action<Patient>? RequestBookAppointment;

    // ── Commands ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void BookAppointment(Patient? p)
    {
        var target = p ?? SelectedPatient;
        if (target == null) { StatusMessage = "Select a patient first."; return; }
        RequestBookAppointment?.Invoke(target);
    }

    [RelayCommand]
    private void RemoveDrug(PrescriptionItemRow? row) { if (row != null) SelectedDrugs.Remove(row); }

    [RelayCommand]
    private void AddDrug()
    {
        if (SelectedDrug == null) { StatusMessage = "Select a medicine from the product catalog."; return; }
        if (DrugQuantity <= 0) { StatusMessage = "Enter a valid medicine quantity."; return; }
        if (DrugQuantity > SelectedDrug.Stock) { StatusMessage = $"Insufficient stock. Available: {SelectedDrug.Stock}."; return; }
        if (SelectedDrugs.Any(x => x.ProductID == SelectedDrug.ProductID)) { StatusMessage = "Medicine is already added."; return; }

        SelectedDrugs.Add(new PrescriptionItemRow
        {
            ProductID = SelectedDrug.ProductID,
            ProductName = SelectedDrug.Name,
            Quantity = DrugQuantity,
            Dosage = Dosage,
            AvailableStock = SelectedDrug.Stock
        });

        Prescription = string.Join(Environment.NewLine, SelectedDrugs.Select(x => $"{x.ProductName} x{x.Quantity} {x.Dosage}".Trim()));
        SelectedDrug = null;
        DrugSearch = string.Empty;
        DrugQuantity = 1;
        Dosage = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ViewSpecific(Patient? p) => View(p);

    [RelayCommand]
    private void DeleteSpecific(Patient? p) => DeleteCommand.Execute(p);

    [RelayCommand]
    private void MarkAsVisited(Patient? p)
    {
        if (p == null) return;
        StatusMessage = $"{p.Name} marked as visited.";
    }

    [RelayCommand]
    private void MarkAsCancelled(Patient? p)
    {
        if (p == null) return;
        StatusMessage = $"{p.Name} marked as cancelled.";
    }

    [RelayCommand]
    private void RequestDeleteAll() => ShowDeleteAllConfirm = true;

    [RelayCommand]
    private async Task AddAnother() { await SaveAsync(); if (Mode == FormMode.View) New(); }

    [RelayCommand]
    private void EditSpecific(Patient? p) => Edit(p);

    [RelayCommand]
    private async Task PrintClinicalSummaryAsync()
    {
        var prescription = BuildPrescriptionFromSelectedDrugs();
        if (prescription == null) return;
        if (await PrescriptionPrintService.ExportAsync(prescription))
        {
            StatusMessage = "Prescription PDF is ready to print.";
        }
    }

    [RelayCommand]
    private async Task PostAndSyncAsync()
    {
        var prescription = BuildPrescriptionFromSelectedDrugs();
        if (prescription == null) return;

        try
        {
            await Task.Run(() => _prescriptionRepo.Insert(prescription, "SentToPharmacy"));
            StatusMessage = "Prescription sent to pharmacist.";
            WeakReferenceMessenger.Default.Send(new PrescriptionHandoffChangedMessage());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to send prescription: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelDeleteAll() => ShowDeleteAllConfirm = false;

    [RelayCommand]
    private async Task ConfirmDeleteAll()
    {
        ShowDeleteAllConfirm = false;
        try { await Task.Run(() => _repo.SoftDeleteAll()); await InitializeAsync(); StatusMessage = "All patients archived."; }
        catch (Exception ex) { StatusMessage = $"Error archiving: {ex.Message}"; }
    }

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        IsReadOnly = false;
        NotifyButtonStates();
        StatusMessage = "Enter new patient details and click Save.";
    }

    [RelayCommand]
    private void Edit(Patient? p)
    {
        var target = p ?? SelectedPatient;
        if (target == null) { StatusMessage = "Select a patient first."; return; }
        SelectedPatient = target;
        FillFields(target);
        Mode = FormMode.Edit;
        IsReadOnly = false;
        NotifyButtonStates();
        _ = LoadVisitHistoryAsync(target.PatientID);
        StatusMessage = "Edit patient details and click Save.";
    }

    [RelayCommand]
    private void View(Patient? p)
    {
        var target = p ?? SelectedPatient;
        if (target == null) { StatusMessage = "Select a patient first."; return; }
        SelectedPatient = target;
        FillFields(target);
        Mode = FormMode.Edit;
        IsReadOnly = true;
        NotifyButtonStates();
        _ = LoadVisitHistoryAsync(target.PatientID);
        StatusMessage = "Viewing patient details.";
    }

    [RelayCommand]
    private async Task DeleteAsync(Patient? p)
    {
        var target = p ?? SelectedPatient;
        if (target == null) { StatusMessage = "Select a patient first."; return; }
        try
        {
            var ok = await Task.Run(() => _repo.Delete(target.PatientID));
            if (ok)
            {
                StatusMessage = "Patient deleted successfully.";
                await InitializeAsync();
                SelectedPatient = null;
            }
            else
            {
                StatusMessage = "Cannot delete patient with active appointments or billing records.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting patient: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Patient Name is required."; return; }
        if (AppointmentDate == default) { StatusMessage = "Appointment Date is required."; return; }
        if (!decimal.TryParse(ConsultationFee, out var fee) || fee < 0)
        {
            StatusMessage = "Check-up Fee must be a valid numeric value greater than or equal to 0.";
            return;
        }

        var p = BuildPatient();
        try
        {
            await Task.Run(() =>
            {
                if (Mode == FormMode.Add) _repo.Insert(p);
                else { p.PatientID = SelectedPatient!.PatientID; _repo.Update(p); }
            });

            StatusMessage = Mode == FormMode.Add ? "Patient saved successfully." : "Patient updated successfully.";
            Mode = FormMode.View;
            NotifyButtonStates();
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving patient: {ex.Message}";
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
    partial void OnSelectedPatientChanged(Patient? value) => OnPropertyChanged(nameof(CanUsePrescriptionActions));

    // ── Helpers ────────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll());
            var drugs = await Task.Run(() => _productRepo.GetPrescribable());
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                Patients = new ObservableCollection<Patient>(list);
                AvailableDrugs = new ObservableCollection<Product>(drugs);
                FilterDrugs();
                FilterPatients();

                TotalPatientsCount = Patients.Count;
                ActiveThisMonthCount = Patients.Count(p => p.ConsultationFee > 0);
                WaitingTodayCount = Math.Max(0, Patients.Count / 10 + 1);
                if (Patients.Count > 0)
                {
                    var avg = Patients.Average(p => p.ConsultationFee);
                    AvgConsultationFee = $"Rs. {avg:N2}";
                }
                else
                {
                    AvgConsultationFee = "Rs. 0.00";
                }
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                StatusMessage = $"Error loading patients: {ex.Message}";
            });
        }
    }

    partial void OnDrugSearchChanged(string value) => FilterDrugs();

    private void FilterDrugs()
    {
        var term = DrugSearch?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
        {
            FilteredDrugs = new ObservableCollection<Product>(AvailableDrugs);
            return;
        }

        FilteredDrugs = new ObservableCollection<Product>(
            AvailableDrugs.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.GenericName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Type?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Packing?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.ProductCodeDisplay.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ProductID.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.PCode.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)));
    }


    private void FilterPatients()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredPatients = new ObservableCollection<Patient>(Patients);
        else
        {
            var term = SearchTerm.ToLower();
            FilteredPatients = new ObservableCollection<Patient>(
                Patients.Where(p => p.Name.ToLower().Contains(term)
                                 || (p.Phone?.ToLower().Contains(term) ?? false)
                                 || (p.Address?.ToLower().Contains(term) ?? false)
                                 || (p.Diagnosis?.ToLower().Contains(term) ?? false)));
        }
        OnPropertyChanged(nameof(IsListEmpty));
    }

    private void ClearFields()
    {
        Name = string.Empty; Age = string.Empty; Gender = "Male";
        Phone = string.Empty; Address = string.Empty;
        CNIC = string.Empty; ReasonOfVisit = string.Empty;
        NextAppointmentTime = null;
        Diagnosis = string.Empty; Prescription = string.Empty;
        ConsultationFee = "0.00"; Discount = "0.00";
        AppointmentDate = DateTimeOffset.Now;
        AppointmentTime = DateTime.Now.TimeOfDay;
        SelectedDrugs.Clear(); VisitHistory.Clear();
        DrugSearch = string.Empty; SelectedDrug = null; FilterDrugs();
    }

    private void FillFields(Patient p)
    {
        Name = p.Name; Age = p.Age?.ToString() ?? string.Empty;
        Gender = p.Gender ?? "Male"; Phone = p.Phone ?? string.Empty;
        Address = p.Address ?? string.Empty;
        CNIC = p.CNIC ?? string.Empty;
        ReasonOfVisit = p.ReasonOfVisit ?? string.Empty;
        NextAppointmentTime = p.NextAppointmentTime;
        Diagnosis = p.Diagnosis ?? string.Empty; Prescription = p.Prescription ?? string.Empty;
        ConsultationFee = p.ConsultationFee.ToString("F2"); Discount = p.Discount.ToString("F2");
        AppointmentDate = p.AppointmentDate.HasValue ? new DateTimeOffset(p.AppointmentDate.Value) : DateTimeOffset.Now;
        AppointmentTime = p.AppointmentTime ?? DateTime.Now.TimeOfDay;
        DrugSearch = string.Empty;
        SelectedDrug = null;
        FilterDrugs();
    }

    private Patient BuildPatient() => new()
    {
        Name = Name, Age = int.TryParse(Age, out var a) ? a : null,
        Gender = Gender, Phone = Phone, Address = Address,
        CNIC = CNIC, ReasonOfVisit = ReasonOfVisit,
        NextAppointmentTime = NextAppointmentTime,
        Diagnosis = Diagnosis, Prescription = Prescription,
        ConsultationFee = decimal.TryParse(ConsultationFee, out var f) ? f : 0,
        Discount = decimal.TryParse(Discount, out var d) ? d : 0,
        AppointmentDate = AppointmentDate.Date,
        AppointmentTime = AppointmentTime
    };

    private async Task LoadVisitHistoryAsync(int patientId)
    {
        try
        {
            var history = await Task.Run(() => _prescriptionRepo.GetByPatient(patientId).ToList());
            foreach (var prescription in history)
            {
                var full = await Task.Run(() => _prescriptionRepo.GetByIdWithItems(prescription.PrescriptionID));
                if (full?.Items != null) prescription.Items = full.Items;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                VisitHistory = new ObservableCollection<Prescription>(history));
        }
        catch
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => VisitHistory = new ObservableCollection<Prescription>());
        }
    }

    private Prescription? BuildPrescriptionFromSelectedDrugs()
    {
        if (SelectedDrug != null)
        {
            AddDrug();
            if (SelectedDrug != null) return null;
        }

        var target = SelectedPatient;
        if (target == null && Mode == FormMode.Edit) target = BuildPatient();
        if (target == null || target.PatientID <= 0)
        {
            StatusMessage = "Save the patient before printing or sending a prescription.";
            return null;
        }
        if (!SelectedDrugs.Any())
        {
            StatusMessage = "Add at least one medicine from the product catalog.";
            return null;
        }
        if (CurrentUser == null)
        {
            StatusMessage = "A logged-in doctor is required to create a prescription.";
            return null;
        }

        return new Prescription
        {
            PatientID = target.PatientID,
            PatientName = Name,
            PatientAge = int.TryParse(Age, out var age) ? age : target.Age,
            PatientGender = Gender,
            PatientPhone = Phone,
            DoctorID = CurrentUser.UserID,
            DoctorName = CurrentUser.FullName,
            VisitDate = DateTime.Now,
            Diagnosis = ReasonOfVisit,
            Notes = Diagnosis,
            Items = SelectedDrugs.Select(x => new PrescriptionItem
            {
                ProductID = x.ProductID,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                Dosage = x.Dosage
            }).ToList()
        };
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsListViewVisible));
        OnPropertyChanged(nameof(PkEditable));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(CanSavePatient));
        OnPropertyChanged(nameof(CanUsePrescriptionActions));
    }
}
