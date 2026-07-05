using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Patients;

public enum FormMode { View, Add, Edit }

public partial class PatientRegistryViewModel : ViewModelBase
{
    private readonly PatientRepository _repo;

    public PatientRegistryViewModel(PatientRepository repo)
    {
        _repo = repo;
        LoadPatients();
    }

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    // ── Button visibility ──────────────────────────────────────────────────
    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool PkEditable => Mode == FormMode.Add;

    // ── Data ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();
    [ObservableProperty] private Patient? _selectedPatient;

    // Edit fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _age = string.Empty;
    [ObservableProperty] private string _gender = "Male";
    [ObservableProperty] private string _contact = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _medicalHistory = string.Empty;

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
    private void Edit()
    {
        if (SelectedPatient == null) { StatusMessage = "Select a patient first."; return; }
        FillFields(SelectedPatient);
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Edit patient details and click Save.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedPatient == null) { StatusMessage = "Select a patient first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedPatient.PatientID));
        if (ok) { StatusMessage = "Patient deleted."; LoadPatients(); SelectedPatient = null; }
        else StatusMessage = "Cannot delete — patient has existing prescriptions.";
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
        Mode = FormMode.View;
        NotifyButtonStates();
        LoadPatients();
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
    private void LoadPatients()
    {
        var list = _repo.GetAll();
        Patients = new ObservableCollection<Patient>(list);
        FilterPatients();
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
                                 || (p.Contact?.ToLower().Contains(term) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Age = string.Empty; Gender = "Male";
        Contact = string.Empty; Address = string.Empty; MedicalHistory = string.Empty;
    }

    private void FillFields(Patient p)
    {
        Name = p.Name; Age = p.Age?.ToString() ?? string.Empty;
        Gender = p.Gender ?? "Male"; Contact = p.Contact ?? string.Empty;
        Address = p.Address ?? string.Empty; MedicalHistory = p.MedicalHistory ?? string.Empty;
    }

    private Patient BuildPatient() => new()
    {
        Name = Name, Age = int.TryParse(Age, out var a) ? a : null,
        Gender = Gender, Contact = Contact, Address = Address, MedicalHistory = MedicalHistory
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(PkEditable));
    }
}
