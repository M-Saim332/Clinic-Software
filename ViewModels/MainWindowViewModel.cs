using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Medicines;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.ViewModels.Users;
using ClinicSystem.UI.ViewModels.Reports;
using ClinicSystem.UI.ViewModels.Companies;
using ClinicSystem.UI.ViewModels.Suppliers;

using ClinicSystem.UI.ViewModels.Appointments;
using ClinicSystem.UI.ViewModels.Purchases;
using ClinicSystem.UI.ViewModels.Sales;
using ClinicSystem.UI.ViewModels.Inventory;
using ClinicSystem.UI.ViewModels.Dashboard;
using ClinicSystem.Data;
using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ClinicSystem.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // ── Injected ViewModels ────────────────────────────────────────────────
    private readonly DashboardViewModel        _dashboardVM;
    private readonly PatientRegistryViewModel  _patientVM;
    private readonly MedicineRegistryViewModel _medicineVM;
    private readonly PrescriptionViewModel     _prescriptionVM;
    private readonly VisitHistoryViewModel     _visitHistoryVM;
    private readonly UserRegistryViewModel     _userVM;
    private readonly ReportsViewModel          _reportsVM;
    private readonly CompanyRegistryViewModel  _companyVM;
    private readonly SupplierRegistryViewModel _supplierVM;

    private readonly AppointmentViewModel      _appointmentVM;
    private readonly PurchaseViewModel         _purchaseVM;
    private readonly SaleViewModel             _saleVM;
    private readonly MedicineReturnViewModel   _returnVM;
    private readonly InventoryViewModel        _inventoryVM;
    private readonly DatabaseSession           _dbSession;

    public ChangePasswordViewModel ChangePasswordVM { get; }

    public MainWindowViewModel(
        DashboardViewModel        dashboardVM,
        PatientRegistryViewModel  patientVM,
        MedicineRegistryViewModel medicineVM,
        PrescriptionViewModel     prescriptionVM,
        VisitHistoryViewModel     visitHistoryVM,
        UserRegistryViewModel     userVM,
        ReportsViewModel          reportsVM,
        CompanyRegistryViewModel  companyVM,
        SupplierRegistryViewModel supplierVM,

        AppointmentViewModel      appointmentVM,
        PurchaseViewModel         purchaseVM,
        SaleViewModel             saleVM,
        MedicineReturnViewModel   returnVM,
        InventoryViewModel        inventoryVM,
        ChangePasswordViewModel   changePasswordVM,
        DatabaseSession           dbSession)
    {
        _dashboardVM    = dashboardVM;
        _patientVM      = patientVM;
        _medicineVM     = medicineVM;
        _prescriptionVM = prescriptionVM;
        _visitHistoryVM = visitHistoryVM;
        _userVM         = userVM;
        _reportsVM      = reportsVM;
        _companyVM      = companyVM;
        _supplierVM     = supplierVM;

        _appointmentVM  = appointmentVM;
        _purchaseVM     = purchaseVM;
        _saleVM         = saleVM;
        _returnVM       = returnVM;
        _inventoryVM    = inventoryVM;
        _dbSession      = dbSession;

        ChangePasswordVM = changePasswordVM;
        ChangePasswordVM.CloseRequested += () => ShowChangePassword = false;

        // Start on Dashboard
        NavigateTo(_dashboardVM, "Dashboard");

        // Startup data load
        IsLoading = true;
        Task.Run(async () =>
        {
            try
            {
                await _patientVM.InitializeAsync();
                await _medicineVM.InitializeAsync();
                await _prescriptionVM.InitializeAsync();
                await _userVM.InitializeAsync();
                await _companyVM.InitializeAsync();
                await _dashboardVM.InitializeAsync();
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

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ViewModelBase? _currentPageViewModel;
    [ObservableProperty] private string _statusText   = string.Empty;
    [ObservableProperty] private string _pageTitle    = "Dashboard";
    [ObservableProperty] private bool   _isLoading;

    public string TodayDate        => DateTime.Now.ToString("dddd, MMM dd yyyy");
    public string CurrentUserName  => CurrentUser?.FullName.Length > 0 ? CurrentUser.FullName : CurrentUser?.Username ?? "Unknown";
    public string CurrentUserRole  => CurrentUser?.Role ?? string.Empty;
    public bool IsAdmin            => CurrentUser?.IsAdmin ?? false;

    // ── Active nav flags (for sidebar highlight) ───────────────────────────
    [ObservableProperty] private bool _isDashboardActive;
    [ObservableProperty] private bool _isPatientsActive;
    [ObservableProperty] private bool _isMedicinesActive;
    [ObservableProperty] private bool _isCompaniesActive;
    [ObservableProperty] private bool _isSuppliersActive;

    [ObservableProperty] private bool _isPurchasesActive;
    [ObservableProperty] private bool _isSalesActive;
    [ObservableProperty] private bool _isReturnsActive;
    [ObservableProperty] private bool _isInventoryActive;
    [ObservableProperty] private bool _isAppointmentsActive;
    [ObservableProperty] private bool _isPrescriptionsActive;
    [ObservableProperty] private bool _isUsersActive;
    [ObservableProperty] private bool _isReportsActive;

    [ObservableProperty] private bool _showChangePassword;

    private bool _visitHistoryLoaded;

    // ── Navigation helper ──────────────────────────────────────────────────
    private void NavigateTo(ViewModelBase vm, string title)
    {
        CurrentPageViewModel = vm;
        PageTitle = title;
        ClearActiveFlags();
        switch (title)
        {
            case "Dashboard":    IsDashboardActive    = true; break;
            case "Patients":     IsPatientsActive     = true; break;
            case "Medicines":    IsMedicinesActive    = true; break;
            case "Companies":    IsCompaniesActive    = true; break;
            case "Suppliers":    IsSuppliersActive    = true; break;

            case "Purchases":    IsPurchasesActive    = true; break;
            case "Sales":        IsSalesActive        = true; break;
            case "Returns":      IsReturnsActive      = true; break;
            case "Inventory":    IsInventoryActive    = true; break;
            case "Appointments": IsAppointmentsActive = true; break;
            case "New Visit":    IsPrescriptionsActive = true; break;
            case "Users":        IsUsersActive        = true; break;
            case "Reports":      IsReportsActive      = true; break;
        }
    }

    private void ClearActiveFlags()
    {
        IsDashboardActive = IsPatientsActive = IsMedicinesActive = IsCompaniesActive =
        IsSuppliersActive = IsPurchasesActive = IsSalesActive = IsReturnsActive =
        IsInventoryActive = IsAppointmentsActive = IsPrescriptionsActive =
        IsUsersActive = IsReportsActive = false;
    }

    // ── Navigation commands ────────────────────────────────────────────────
    [RelayCommand] private void ShowDashboard()    { NavigateTo(_dashboardVM,    "Dashboard");    _ = _dashboardVM.InitializeAsync(); }
    [RelayCommand] private void ShowPatients()     { NavigateTo(_patientVM,      "Patients"); }
    [RelayCommand] private void ShowMedicines()    { NavigateTo(_medicineVM,     "Medicines"); }
    [RelayCommand] private void ShowCompanies()    { NavigateTo(_companyVM,      "Companies"); }
    [RelayCommand] private void ShowSuppliers()    { NavigateTo(_supplierVM,     "Suppliers");    _ = _supplierVM.InitializeAsync(); }

    [RelayCommand] private void ShowPurchases()    { NavigateTo(_purchaseVM,     "Purchases");    _ = _purchaseVM.InitializeAsync(); }
    [RelayCommand] private void ShowSales()        { NavigateTo(_saleVM,         "Sales");        _ = _saleVM.InitializeAsync(); }
    [RelayCommand] private void ShowReturns()      { NavigateTo(_returnVM,       "Returns"); }
    [RelayCommand] private void ShowInventory()    { NavigateTo(_inventoryVM,    "Inventory");    _ = _inventoryVM.InitializeAsync(); }
    [RelayCommand] private void ShowAppointments() { NavigateTo(_appointmentVM, "Appointments"); _ = _appointmentVM.InitializeAsync(); }
    [RelayCommand] private void ShowUsers()        { NavigateTo(_userVM,         "Users"); }
    [RelayCommand] private void ShowReports()      { NavigateTo(_reportsVM,      "Reports"); }

    [RelayCommand]
    private void ShowPrescriptions()
    {
        NavigateTo(_prescriptionVM, "New Visit");
    }

    [RelayCommand]
    private void ShowVisitHistory()
    {
        NavigateTo(_visitHistoryVM, "Visit History");
        if (!_visitHistoryLoaded)
        {
            _visitHistoryLoaded = true;
            _ = _visitHistoryVM.LoadAllVisits();
        }
    }

    public event Action? LogoutRequested;
    [RelayCommand] private void Logout() => LogoutRequested?.Invoke();

    [RelayCommand]
    private void OpenChangePasswordDialog()
    {
        ChangePasswordVM.Reset();
        ShowChangePassword = true;
    }

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
