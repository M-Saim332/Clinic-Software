using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Appointments;

public partial class AppointmentViewModel : ViewModelBase
{
    private readonly AppointmentRepository _repo;
    private readonly PatientRepository _patientRepo;
    private readonly UserRepository _userRepo;

    public AppointmentViewModel(AppointmentRepository repo, PatientRepository patientRepo, UserRepository userRepo)
    {
        _repo = repo;
        _patientRepo = patientRepo;
        _userRepo = userRepo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Appointment> _appointments = new();
    [ObservableProperty]
    private ObservableCollection<Patient> _patients = new();
    [ObservableProperty]
    private ObservableCollection<User> _doctors = new();
    [ObservableProperty]
    private Appointment? _selectedAppointment;

    // Fields
    [ObservableProperty]
    private Patient? _selectedPatient;
    [ObservableProperty]
    private User? _selectedDoctor;
    [ObservableProperty]
    private DateTimeOffset _appointmentDate = DateTimeOffset.Now;
    [ObservableProperty]
    private TimeSpan _appointmentTime = DateTime.Now.TimeOfDay;
    [ObservableProperty]
    private string _reason = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Book a new appointment.";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedAppointment == null) { StatusMessage = "Select an appointment first."; return; }
        FillFields(SelectedAppointment);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedPatient == null || SelectedDoctor == null)
        {
            StatusMessage = "Patient and Doctor are required.";
            return;
        }

        var appt = new Appointment
        {
            PatientID = SelectedPatient.PatientID,
            DoctorID = SelectedDoctor.UserID,
            AppointmentDate = AppointmentDate.Date,
            AppointmentTime = AppointmentTime,
            Reason = Reason,
            Status = Mode == FormMode.Add ? "Scheduled" : SelectedAppointment!.Status
        };

        if (Mode == FormMode.Add)
        {
            // Conflict check
            if (await Task.Run(() => _repo.CheckConflict(appt.DoctorID, appt.AppointmentDate, appt.AppointmentTime, 0)))
            {
                StatusMessage = "Conflict: Doctor already has an appointment at this time.";
                return;
            }

            await Task.Run(() => _repo.Insert(appt));
            StatusMessage = "Appointment booked.";
        }
        else
        {
            appt.AppointmentID = SelectedAppointment!.AppointmentID;
            
            // Conflict check
            if (await Task.Run(() => _repo.CheckConflict(appt.DoctorID, appt.AppointmentDate, appt.AppointmentTime, appt.AppointmentID)))
            {
                StatusMessage = "Conflict: Doctor already has an appointment at this time.";
                return;
            }

            await Task.Run(() => _repo.Update(appt));
            StatusMessage = "Appointment updated.";
        }
        Mode = FormMode.View;
        NotifyButtonStates();
        await InitializeAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = string.Empty;
    }

    // Status management commands
    [RelayCommand]
    private async Task CheckInAsync()
    {
        if (SelectedAppointment == null) { StatusMessage = "Select an appointment."; return; }
        if (SelectedAppointment.Status != "Scheduled") { StatusMessage = "Only Scheduled appointments can be checked in."; return; }
        
        await Task.Run(() => _repo.UpdateStatus(SelectedAppointment.AppointmentID, "Checked-In", null));
        StatusMessage = "Patient checked in.";
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (SelectedAppointment == null) { StatusMessage = "Select an appointment."; return; }
        if (SelectedAppointment.Status != "Checked-In") { StatusMessage = "Only Checked-In appointments can be completed."; return; }
        
        await Task.Run(() => _repo.UpdateStatus(SelectedAppointment.AppointmentID, "Completed", null));
        StatusMessage = "Appointment completed.";
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task MarkCancelledAsync()
    {
        if (SelectedAppointment == null) { StatusMessage = "Select an appointment."; return; }
        if (SelectedAppointment.Status == "Completed") { StatusMessage = "Cannot cancel completed appointments."; return; }
        
        await Task.Run(() => _repo.UpdateStatus(SelectedAppointment.AppointmentID, "Cancelled", "Cancelled by user"));
        StatusMessage = "Appointment cancelled.";
        await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        var patients = await Task.Run(() => _patientRepo.GetAll());
        var users = await Task.Run(() => _userRepo.GetAll());
        var doctors = users.Where(u => u.Role == "Doctor").ToList();
        var appointments = await Task.Run(() => _repo.GetAll()); // Needs to get names via join
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            Patients = new ObservableCollection<Patient>(patients);
            Doctors = new ObservableCollection<User>(doctors);
            Appointments = new ObservableCollection<Appointment>(appointments.OrderBy(a => a.AppointmentDate).ThenBy(a => a.AppointmentTime));
        });
    }

    private void ClearFields()
    {
        SelectedPatient = null;
        SelectedDoctor = Doctors.FirstOrDefault(); // default to first doc
        AppointmentDate = DateTimeOffset.Now;
        AppointmentTime = DateTime.Now.TimeOfDay;
        Reason = string.Empty;
    }

    private void FillFields(Appointment a)
    {
        SelectedPatient = Patients.FirstOrDefault(p => p.PatientID == a.PatientID);
        SelectedDoctor = Doctors.FirstOrDefault(d => d.UserID == a.DoctorID);
        AppointmentDate = new DateTimeOffset(a.AppointmentDate);
        AppointmentTime = a.AppointmentTime;
        Reason = a.Reason ?? string.Empty;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }
}
