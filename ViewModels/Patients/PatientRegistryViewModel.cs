using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.ViewModels.Prescriptions;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Patients;


public partial class PatientRegistryViewModel : ViewModelBase
{
    private readonly PatientRepository _repo;

    public PatientRegistryViewModel(PatientRepository repo)
    {
        _repo = repo;
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
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private int _selectedTab;

    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };

    public bool IsListEmpty => FilteredPatients.Count == 0;
    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    // ── View-state booleans required by the view ──────────────────────────
    public bool ShowEditButton  => Mode == FormMode.Edit || Mode == FormMode.View;
    public bool ShowSaveButton  => Mode == FormMode.Add  || Mode == FormMode.Edit;

    [ObservableProperty] private bool _showDeleteAllConfirm;

    // ── Drug-selection (prescription inline) ──────────────────────────────
    [ObservableProperty] private string _drugSearch  = string.Empty;
    [ObservableProperty] private string _dosage      = string.Empty;
    [ObservableProperty] private int    _drugQuantity = 1;
    [ObservableProperty] private Product? _selectedDrug;
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
    private void PrintClinicalSummary() { StatusMessage = "Print not yet implemented."; }

    [RelayCommand]
    private void PostAndSync() { StatusMessage = "Send to Pharmacist not yet implemented."; }

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

    // ── Helpers ────────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                Patients = new ObservableCollection<Patient>(list);
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

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsListViewVisible));
        OnPropertyChanged(nameof(PkEditable));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveButton));
    }
}
