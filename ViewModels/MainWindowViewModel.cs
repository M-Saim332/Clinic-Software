using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Medicines;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.ViewModels.Users;
using ClinicSystem.UI.ViewModels.Reports;
using ClinicSystem.Data;
using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ClinicSystem.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // Child ViewModels (injected via DI)
    private readonly PatientRegistryViewModel _patientVM;
    private readonly MedicineRegistryViewModel _medicineVM;
    private readonly PrescriptionViewModel _prescriptionVM;
    private readonly VisitHistoryViewModel _visitHistoryVM;
    private readonly UserRegistryViewModel _userVM;
    private readonly ReportsViewModel _reportsVM;
    private readonly DatabaseSession _dbSession;

    public MainWindowViewModel(
        PatientRegistryViewModel patientVM,
        MedicineRegistryViewModel medicineVM,
        PrescriptionViewModel prescriptionVM,
        VisitHistoryViewModel visitHistoryVM,
        UserRegistryViewModel userVM,
        ReportsViewModel reportsVM,
        DatabaseSession dbSession)
    {
        _patientVM = patientVM;
        _medicineVM = medicineVM;
        _prescriptionVM = prescriptionVM;
        _visitHistoryVM = visitHistoryVM;
        _userVM = userVM;
        _reportsVM = reportsVM;
        _dbSession = dbSession;

        // Start on patient registry
        CurrentPageViewModel = _patientVM;

        // Load sequentially to avoid overwhelming the SQL connection pool.
        // VisitHistory is excluded from startup — it loads lazily on first tab visit.
        IsLoading = true;
        Task.Run(async () =>
        {
            try
            {
                await _patientVM.InitializeAsync();
                await _medicineVM.InitializeAsync();
                await _prescriptionVM.InitializeAsync();
                await _userVM.InitializeAsync();
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusText = $"Startup load failed: {ex.Message}");
            }
            finally
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
            }
        });
    }

    [ObservableProperty] private ViewModelBase? _currentPageViewModel;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    private bool _visitHistoryLoaded;

    public string LoggedInUser => CurrentUser != null
        ? $"{CurrentUser.DisplayName}  [{CurrentUser.Role}]"
        : "Not logged in";

    // Navigation commands
    [RelayCommand] private void ShowPatients()      { CurrentPageViewModel = _patientVM; }
    [RelayCommand] private void ShowMedicines()     { CurrentPageViewModel = _medicineVM; }
    [RelayCommand] private void ShowPrescriptions() { CurrentPageViewModel = _prescriptionVM; }
    [RelayCommand] private void ShowUsers()         { CurrentPageViewModel = _userVM; }
    [RelayCommand] private void ShowReports()       { CurrentPageViewModel = _reportsVM; }

    [RelayCommand]
    private void ShowVisitHistory()
    {
        CurrentPageViewModel = _visitHistoryVM;
        // Lazy-load visit history only on first navigation to this tab
        if (!_visitHistoryLoaded)
        {
            _visitHistoryLoaded = true;
            _ = _visitHistoryVM.LoadAllVisits();
        }
    }

    public event Action? LogoutRequested;
    [RelayCommand] private void Logout() => LogoutRequested?.Invoke();

    [RelayCommand]
    private async Task BackupAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storage = desktop.MainWindow?.StorageProvider;
            if (storage == null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Backup Database",
                SuggestedFileName = $"ClinicDB_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                DefaultExtension = "bak",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("SQL Server Backup (*.bak)") { Patterns = new[] { "*.bak" } }
                }
            });

            if (file != null)
            {
                StatusText = "Backing up database...";
                try
                {
                    await Task.Run(() => _dbSession.Backup(file.Path.LocalPath));
                    StatusText = "Backup completed successfully!";
                }
                catch (Exception ex)
                {
                    StatusText = $"Backup failed: {ex.Message}";
                }
            }
        }
    }
}
