using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ClinicSystem.UI.ViewModels.Search;

public partial class SearchViewModel : ViewModelBase
{
    private readonly PatientRepository _patientRepo;
    private readonly AppointmentRepository _appointmentRepo;
    private readonly MedicineRepository _medicineRepo;

    public SearchViewModel(
        PatientRepository patientRepo,
        AppointmentRepository appointmentRepo,
        MedicineRepository medicineRepo)
    {
        _patientRepo = patientRepo;
        _appointmentRepo = appointmentRepo;
        _medicineRepo = medicineRepo;
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Appointment> _appointments = new();
    [ObservableProperty] private ObservableCollection<Medicine> _medicines = new();

    [ObservableProperty] private int _patientCount;
    [ObservableProperty] private int _appointmentCount;
    [ObservableProperty] private int _medicineCount;

    partial void OnSearchTextChanged(string value)
    {
        _ = PerformSearchAsync(value);
    }

    private async Task PerformSearchAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            Patients.Clear();
            Appointments.Clear();
            Medicines.Clear();
            PatientCount = AppointmentCount = MedicineCount = 0;
            return;
        }

        IsSearching = true;

        try
        {
            var query = text.ToLowerInvariant();

            var patientTask = Task.Run(() => _patientRepo.GetAll());
            var appointmentTask = Task.Run(() => _appointmentRepo.GetAll());
            var medicineTask = Task.Run(() => _medicineRepo.GetAll());

            await Task.WhenAll(patientTask, appointmentTask, medicineTask);

            var patientsList = patientTask.Result.Where(p => 
                (p.Name?.ToLowerInvariant().Contains(query) ?? false) || 
                (p.Phone?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            var appointmentsList = appointmentTask.Result.Where(a => 
                (a.PatientName?.ToLowerInvariant().Contains(query) ?? false) || 
                (a.DoctorName?.ToLowerInvariant().Contains(query) ?? false) || 
                (a.Reason?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            var medicinesList = medicineTask.Result.Where(m => 
                (m.Name?.ToLowerInvariant().Contains(query) ?? false) || 
                (m.GenericName?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Patients = new ObservableCollection<Patient>(patientsList);
                Appointments = new ObservableCollection<Appointment>(appointmentsList);
                Medicines = new ObservableCollection<Medicine>(medicinesList);

                PatientCount = patientsList.Count;
                AppointmentCount = appointmentsList.Count;
                MedicineCount = medicinesList.Count;
            });
        }
        catch (Exception)
        {
            // Ignore gracefully
        }
        finally
        {
            IsSearching = false;
        }
    }
}
