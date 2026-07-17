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
    private readonly ProductRepository _productRepo;

    public SearchViewModel(
        PatientRepository patientRepo,
        AppointmentRepository appointmentRepo,
        ProductRepository productRepo)
    {
        _patientRepo = patientRepo;
        _appointmentRepo = appointmentRepo;
        _productRepo = productRepo;
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Appointment> _appointments = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();

    [ObservableProperty] private int _patientCount;
    [ObservableProperty] private int _appointmentCount;
    [ObservableProperty] private int _productCount;

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
            Products.Clear();
            PatientCount = AppointmentCount = ProductCount = 0;
            return;
        }

        IsSearching = true;

        try
        {
            var query = text.ToLowerInvariant();

            var patientTask = Task.Run(() => _patientRepo.GetAll());
            var appointmentTask = Task.Run(() => _appointmentRepo.GetAll());
            var productTask = Task.Run(() => _productRepo.GetAll());

            await Task.WhenAll(patientTask, appointmentTask, productTask);

            var patientsList = patientTask.Result.Where(p => 
                (p.Name?.ToLowerInvariant().Contains(query) ?? false) || 
                (p.Phone?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            var appointmentsList = appointmentTask.Result.Where(a => 
                (a.PatientName?.ToLowerInvariant().Contains(query) ?? false) || 
                (a.DoctorName?.ToLowerInvariant().Contains(query) ?? false) || 
                (a.Reason?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            var productsList = productTask.Result.Where(m => 
                (m.Name?.ToLowerInvariant().Contains(query) ?? false) || 
                (m.GenericName?.ToLowerInvariant().Contains(query) ?? false)
            ).ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Patients = new ObservableCollection<Patient>(patientsList);
                Appointments = new ObservableCollection<Appointment>(appointmentsList);
                Products = new ObservableCollection<Product>(productsList);

                PatientCount = patientsList.Count;
                AppointmentCount = appointmentsList.Count;
                ProductCount = productsList.Count;
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
