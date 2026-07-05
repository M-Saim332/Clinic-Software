using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Reports;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly PatientRepository _patientRepo;
    private readonly MedicineRepository _medicineRepo;
    private readonly PrescriptionRepository _prescriptionRepo;

    public ReportsViewModel(PatientRepository patientRepo, MedicineRepository medicineRepo, PrescriptionRepository prescriptionRepo)
    {
        _patientRepo = patientRepo;
        _medicineRepo = medicineRepo;
        _prescriptionRepo = prescriptionRepo;
    }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private ObservableCollection<Patient> _patientList = new();
    [ObservableProperty] private ObservableCollection<Medicine> _medicineStockList = new();
    [ObservableProperty] private ObservableCollection<Medicine> _expiredMedicines = new();
    [ObservableProperty] private ObservableCollection<Medicine> _lowStockMedicines = new();
    [ObservableProperty] private ObservableCollection<Prescription> _allVisits = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [RelayCommand]
    private async Task LoadPatientListAsync()
    {
        IsBusy = true;
        PatientList = new ObservableCollection<Patient>(await Task.Run(_patientRepo.GetAll));
        StatusMessage = $"{PatientList.Count} patients.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadMedicineStockAsync()
    {
        IsBusy = true;
        MedicineStockList = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetAll));
        StatusMessage = $"{MedicineStockList.Count} medicines.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadExpiredLowStockAsync()
    {
        IsBusy = true;
        ExpiredMedicines = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetExpired));
        LowStockMedicines = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetLowStock));
        StatusMessage = $"{ExpiredMedicines.Count} expired, {LowStockMedicines.Count} low-stock.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadAllVisitsAsync()
    {
        IsBusy = true;
        AllVisits = new ObservableCollection<Prescription>(await Task.Run(_prescriptionRepo.GetAll));
        StatusMessage = $"{AllVisits.Count} visits.";
        IsBusy = false;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = value switch
        {
            0 => LoadPatientListAsync(),
            1 => LoadMedicineStockAsync(),
            2 => LoadExpiredLowStockAsync(),
            3 => LoadAllVisitsAsync(),
            _ => Task.CompletedTask
        };
    }
}
