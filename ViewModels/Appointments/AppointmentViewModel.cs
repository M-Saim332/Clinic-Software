using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Appointments;

public partial class AppointmentViewModel : ViewModelBase, ISearchable
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
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(ShowSaveButton))]
    [NotifyPropertyChangedFor(nameof(ShowEditButton))]
    private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<Appointment> _appointments = new();
    private ObservableCollection<Appointment> _allAppointments = new();
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<User> _doctors = new();
    [ObservableProperty] private Appointment? _selectedAppointment;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Appointments...";

    partial void OnSearchTermChanged(string value) => FilterAppointments();

    private void FilterAppointments()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Appointments = new ObservableCollection<Appointment>(_allAppointments);
        }
        else
        {
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            Appointments = new ObservableCollection<Appointment>(
                _allAppointments.Where(a => 
                    (a.PatientName?.ToLower().Contains(term) ?? false) ||
                    (a.AppointmentNo?.ToLower().Contains(term) ?? false) ||
                    (a.Phone?.ToLower().Replace(" ", "").Replace("-", "").Contains(term) ?? false) ||
                    (a.Reason?.ToLower().Contains(term) ?? false)));
        }
    }

    // KPI Summary counts
    [ObservableProperty] private int _totalAppointmentsCount;
    [ObservableProperty] private int _scheduledCount;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _missedCount;

    // Form fields
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private string _patientName = string.Empty;
    [ObservableProperty] private string _patientPhone = string.Empty;
    [ObservableProperty] private string _gender = string.Empty;
    [ObservableProperty] private int? _age;
    // Removed doctor selection, using CurrentUser
    [ObservableProperty] private DateTimeOffset _appointmentDate = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan _appointmentTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _remarks = string.Empty;

    [ObservableProperty] private bool _showCreatePatientPrompt;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool IsReadOnly => Mode == FormMode.Details || Mode == FormMode.View;
    public bool ShowSaveButton => Mode == FormMode.Add || Mode == FormMode.Edit;
    public bool ShowEditButton => Mode == FormMode.Details;

    public void PreselectPatient(Patient p)
    {
        New();
        SelectedPatient = Patients.FirstOrDefault(x => x.PatientID == p.PatientID);
    }

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value != null)
        {
            PatientName = value.Name;
            PatientPhone = value.Phone ?? string.Empty;
            Gender = value.Gender ?? string.Empty;
            Age = value.Age;
        }
        else if (Mode == FormMode.Add)
        {
            PatientName = string.Empty;
            PatientPhone = string.Empty;
            Gender = string.Empty;
            Age = null;
        }
    }

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Book a new appointment.";
    }

    [RelayCommand]
    private void EditSpecific(Appointment a)
    {
        if (a == null) return;
        SelectedAppointment = a;
        FillFields(a);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private void ViewSpecific(Appointment a)
    {
        if (a == null) return;
        SelectedAppointment = a;
        FillFields(a);
        Mode = FormMode.Details;
        NotifyButtonStates();
        StatusMessage = "Viewing appointment details.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {

        var appt = new Appointment
        {
            PatientID = SelectedPatient?.PatientID,
            PatientName = SelectedPatient == null ? PatientName : SelectedPatient.Name,
            Phone = SelectedPatient == null ? PatientPhone : SelectedPatient.Phone,
            Gender = SelectedPatient == null ? Gender : SelectedPatient.Gender,
            Age = SelectedPatient == null ? Age : SelectedPatient.Age,
            DoctorID = ViewModelBase.CurrentUser?.UserID ?? 1,
            AppointmentDate = AppointmentDate.Date,
            AppointmentTime = AppointmentTime,
            Reason = Reason,
            Remarks = Remarks,
            Status = Mode == FormMode.Add ? "Scheduled" : SelectedAppointment!.Status
        };

        try
        {
            if (Mode == FormMode.Add)
            {
                if (await Task.Run(() => _repo.CheckConflict(appt.DoctorID, appt.AppointmentDate, appt.AppointmentTime, 0)))
                {
                    StatusMessage = "Conflict: Doctor already has an appointment at this time.";
                    return;
                }
                await Task.Run(() => _repo.Insert(appt));
                StatusMessage = "Appointment booked.";
                LogActivity("Appointment Created", $"Appointment booked for {appt.PatientName} on {appt.AppointmentDate:dd MMM yyyy}", "Appointments");
            }
            else
            {
                appt.AppointmentID = SelectedAppointment!.AppointmentID;
                if (await Task.Run(() => _repo.CheckConflict(appt.DoctorID, appt.AppointmentDate, appt.AppointmentTime, appt.AppointmentID)))
                {
                    StatusMessage = "Conflict: Doctor already has an appointment at this time.";
                    return;
                }
                await Task.Run(() => _repo.Update(appt));
                StatusMessage = "Appointment updated.";
                LogActivity("Appointment Updated", $"Appointment #{appt.AppointmentID} updated", "Appointments");
            }
            Mode = FormMode.View;
            NotifyButtonStates();
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving appointment: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task CompleteSpecificAsync(Appointment a)
    {
        if (a == null) return;
        if (a.Status != "Scheduled") { StatusMessage = "Only Scheduled appointments can be completed."; return; }
        try
        {
            await Task.Run(() => _repo.UpdateStatus(a.AppointmentID, "Completed", null));
            StatusMessage = "Appointment completed.";
            LogActivity("Appointment Updated", $"Appointment for {a.PatientName} marked Completed", "Appointments");
            await InitializeAsync();

            if (a.PatientID == null)
            {
                SelectedAppointment = a;
                ShowCreatePatientPrompt = true;
            }
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }
    
    [RelayCommand]
    private async Task MarkMissedSpecificAsync(Appointment a)
    {
        if (a == null) return;
        if (a.Status != "Scheduled") { StatusMessage = "Only Scheduled appointments can be marked missed."; return; }
        try
        {
            await Task.Run(() => _repo.UpdateStatus(a.AppointmentID, "Missed", null));
            StatusMessage = "Appointment marked missed.";
            LogActivity("Appointment Updated", $"Appointment for {a.PatientName} marked Missed", "Appointments");
            await InitializeAsync();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task MarkCancelledSpecificAsync(Appointment a)
    {
        if (a == null) return;
        if (a.Status == "Completed" || a.Status == "Missed") { StatusMessage = "Cannot cancel completed/missed appointments."; return; }
        try
        {
            await Task.Run(() => _repo.UpdateStatus(a.AppointmentID, "Cancelled", "Cancelled by user"));
            StatusMessage = "Appointment cancelled.";
            LogActivity("Appointment Updated", $"Appointment for {a.PatientName} Cancelled", "Appointments");
            await InitializeAsync();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }
    
    [RelayCommand]
    private async Task CreatePatientFromAppointmentAsync()
    {
        if (SelectedAppointment == null || SelectedAppointment.PatientID != null) return;
        
        try
        {
            var p = new Patient
            {
                Name = SelectedAppointment.PatientName ?? "Unknown",
                Phone = SelectedAppointment.Phone,
                Gender = SelectedAppointment.Gender ?? "Other",
                Age = SelectedAppointment.Age ?? 0
            };
            int newId = await Task.Run(() => _patientRepo.Insert(p));
            
            var a = _repo.GetById(SelectedAppointment.AppointmentID);
            if (a != null)
            {
                a.PatientID = newId;
                await Task.Run(() => _repo.Update(a));
            }
            
            ShowCreatePatientPrompt = false;
            StatusMessage = "Patient created and linked to appointment.";
            await InitializeAsync();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }
    
    [RelayCommand]
    private void DismissCreatePatientPrompt()
    {
        ShowCreatePatientPrompt = false;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var patients = await Task.Run(() => _patientRepo.GetAll());
            var users = await Task.Run(() => _userRepo.GetAll());
            var doctors = users.Where(u => u.Role == "Doctor").ToList();
            
            // Auto-cleanup old appointments (older than 3 days)
            await Task.Run(() => _repo.CleanupOldAppointments());
            
            var appointments = await Task.Run(() => _repo.GetAll());

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Patients = new ObservableCollection<Patient>(patients);
                Doctors = new ObservableCollection<User>(doctors);
                var sorted = new ObservableCollection<Appointment>(appointments.OrderBy(a => a.AppointmentDate).ThenBy(a => a.AppointmentTime));
                _allAppointments = sorted;
                FilterAppointments();

                TotalAppointmentsCount = Appointments.Count;
                ScheduledCount = Appointments.Count(a => a.Status == "Scheduled");
                CompletedCount = Appointments.Count(a => a.Status == "Completed" && a.AppointmentDate.Date == DateTime.Today);
                MissedCount = Appointments.Count(a => a.Status == "Missed");
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load data: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        SelectedPatient = null;
        PatientName = string.Empty;
        PatientPhone = string.Empty;
        Gender = string.Empty;
        Age = null;
        AppointmentDate = DateTimeOffset.Now;
        AppointmentTime = DateTime.Now.TimeOfDay;
        Reason = string.Empty;
        Remarks = string.Empty;
        ShowCreatePatientPrompt = false;
    }

    private void FillFields(Appointment a)
    {
        SelectedPatient = Patients.FirstOrDefault(p => p.PatientID == a.PatientID);
        PatientName = a.PatientName ?? string.Empty;
        PatientPhone = a.Phone ?? string.Empty;
        Gender = a.Gender ?? string.Empty;
        Age = a.Age;
        AppointmentDate = new DateTimeOffset(a.AppointmentDate);
        AppointmentTime = a.AppointmentTime;
        Reason = a.Reason ?? string.Empty;
        Remarks = a.Remarks ?? string.Empty;
        ShowCreatePatientPrompt = false;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(ShowEditButton));
    }
}
