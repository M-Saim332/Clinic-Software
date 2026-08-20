using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Dashboard;

public partial class ClinicalDashboardViewModel : ViewModelBase
{
    private readonly AppointmentRepository _appointmentRepo;
    private readonly PatientRepository _patientRepo;

    [ObservableProperty] private int _todaysAppointments;
    [ObservableProperty] private int _patientsWaiting;
    [ObservableProperty] private ObservableCollection<Patient> _recentPatients = new();
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ClinicalDashboardViewModel(AppointmentRepository appointmentRepo, PatientRepository patientRepo)
    {
        _appointmentRepo = appointmentRepo;
        _patientRepo = patientRepo;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var appointments = await Task.Run(_appointmentRepo.GetAll);
            var patients = await Task.Run(() => _patientRepo.GetAll("Clinical"));
            var appointmentList = appointments.ToList();
            var patientList = patients.ToList();
            TodaysAppointments = appointmentList.Count(a => a.AppointmentDate.Date == DateTime.Today);
            PatientsWaiting = appointmentList.Count(a => a.AppointmentDate.Date == DateTime.Today &&
                (a.Status == "Scheduled" || a.Status == "Checked-In"));
            RecentPatients = new ObservableCollection<Patient>(patientList.OrderByDescending(p => p.LastVisitDate).Take(8));
        }
        catch (Exception ex) { StatusMessage = $"Clinical dashboard could not be loaded: {ex.Message}"; }
    }
}
