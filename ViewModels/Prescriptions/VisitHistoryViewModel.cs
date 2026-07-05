using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Prescriptions;

public partial class VisitHistoryViewModel : ViewModelBase
{
    private readonly PrescriptionRepository _prescRepo;
    private readonly PatientRepository _patientRepo;

    public VisitHistoryViewModel(PrescriptionRepository prescRepo, PatientRepository patientRepo)
    {
        _prescRepo = prescRepo;
        _patientRepo = patientRepo;
        LoadAllVisits();
    }

    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private ObservableCollection<Prescription> _prescriptions = new();
    [ObservableProperty] private Prescription? _selectedPrescription;
    [ObservableProperty] private ObservableCollection<PrescriptionItem> _prescriptionItems = new();
    [ObservableProperty] private string _patientSearch = string.Empty;
    [ObservableProperty] private bool _showPatientList;
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();

    [RelayCommand] private void ShowPatients() { ShowPatientList = true; FilterPatients(); }
    [RelayCommand] private void ClosePatientList() => ShowPatientList = false;

    [RelayCommand]
    private void SelectPatient(Patient? p)
    {
        if (p == null) return;
        SelectedPatient = p;
        ShowPatientList = false;
        LoadForPatient(p.PatientID);
    }

    [RelayCommand]
    private void LoadAllVisits()
    {
        SelectedPatient = null;
        var all = _prescRepo.GetAll();
        Prescriptions = new ObservableCollection<Prescription>(all);
        Patients = new ObservableCollection<Patient>(_patientRepo.GetAll());
        FilterPatients();
    }

    partial void OnSelectedPrescriptionChanged(Prescription? value)
    {
        if (value == null) { PrescriptionItems.Clear(); return; }
        var full = _prescRepo.GetByIdWithItems(value.PrescriptionID);
        if (full != null)
        {
            PrescriptionItems = new ObservableCollection<PrescriptionItem>(full.Items);
        }
        else
        {
            PrescriptionItems.Clear();
        }
    }

    partial void OnPatientSearchChanged(string value) => FilterPatients();

    private void LoadForPatient(int patientId)
    {
        var list = _prescRepo.GetByPatient(patientId);
        Prescriptions = new ObservableCollection<Prescription>(list);
        PrescriptionItems.Clear();
    }

    private void FilterPatients()
    {
        if (string.IsNullOrWhiteSpace(PatientSearch))
            FilteredPatients = new ObservableCollection<Patient>(Patients);
        else
        {
            var t = PatientSearch.ToLower();
            FilteredPatients = new ObservableCollection<Patient>(
                Patients.Where(p => p.Name.ToLower().Contains(t)));
        }
    }
}
