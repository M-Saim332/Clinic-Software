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
    }

    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private ObservableCollection<Prescription> _prescriptions = new();
    [ObservableProperty] private Prescription? _selectedPrescription;
    [ObservableProperty] private ObservableCollection<PrescriptionItem> _prescriptionItems = new();
    [ObservableProperty] private string _patientSearch = string.Empty;
    [ObservableProperty] private bool _showPatientList;
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();
    [ObservableProperty] private Patient? _selectedFilteredPatient;

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
    public async Task LoadAllVisits()
    {
        SelectedPatient = null;
        var all = await Task.Run(() => _prescRepo.GetAll());
        var pats = await Task.Run(() => _patientRepo.GetAll());
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            Prescriptions = new ObservableCollection<Prescription>(all);
            Patients = new ObservableCollection<Patient>(pats);
            FilterPatients();
        });
    }

    partial void OnSelectedPrescriptionChanged(Prescription? value)
    {
        if (value == null) { PrescriptionItems.Clear(); return; }
        // Run DB query off the UI thread to prevent freezing.
        var prescriptionId = value.PrescriptionID;
        Task.Run(() => _prescRepo.GetByIdWithItems(prescriptionId))
            .ContinueWith(t =>
            {
                var full = t.Result;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (full != null)
                        PrescriptionItems = new ObservableCollection<PrescriptionItem>(full.Items);
                    else
                        PrescriptionItems.Clear();
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    partial void OnPatientSearchChanged(string value) => FilterPatients();

    private void LoadForPatient(int patientId)
    {
        PrescriptionItems.Clear();
        Task.Run(() => _prescRepo.GetByPatient(patientId))
            .ContinueWith(t =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Prescriptions = new ObservableCollection<Prescription>(t.Result);
                    PrescriptionItems.Clear();
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
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
