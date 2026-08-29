using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Products;
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

using ClinicSystem.UI.ViewModels.Search;
using ClinicSystem.UI.ViewModels.Settings;
using ClinicSystem.UI.ViewModels.Profile;
using ClinicSystem.UI.ViewModels.Returns;
using ClinicSystem.Data;
using Dapper;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.Messages;
using CommunityToolkit.Mvvm.Messaging;


namespace ClinicSystem.UI.ViewModels;

public class AppNotification
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public string AccentBrushKey { get; init; } = "BrushPrimaryBlue";
    public bool IsUnread { get; init; }
    public object? Payload { get; init; }
}

public partial class MainWindowViewModel : ViewModelBase, IRecipient<PrescriptionHandoffChangedMessage>, IRecipient<ClinicNameChangedMessage>
{
    // ── Injected ViewModels ────────────────────────────────────────────────
    private readonly DashboardViewModel        _dashboardVM;
    private readonly ClinicalDashboardViewModel _clinicalDashboardVM;
    private readonly PatientRegistryViewModel  _patientVM;
    private readonly ProductRegistryViewModel _productVM;
    private readonly PrescriptionViewModel     _prescriptionVM;
    private readonly VisitHistoryViewModel     _visitHistoryVM;
    private readonly UserRegistryViewModel     _userVM;
    private readonly ReportsViewModel          _reportsVM;
    private readonly ClinicalReportsViewModel  _clinicalReportsVM;
    private readonly CompanyRegistryViewModel  _companyVM;
    private readonly SupplierRegistryViewModel _supplierVM;
    private readonly SearchViewModel            _searchVM;
    private readonly SettingsViewModel          _settingsVM;

    private readonly AppointmentViewModel      _appointmentVM;
    private readonly PurchaseViewModel         _purchaseVM;
    private readonly SaleViewModel             _saleVM;
    private readonly InventoryViewModel        _inventoryVM;
    private readonly ProfileViewModel          _profileVM;
    private readonly ReturnsViewModel          _returnsVM;
    private readonly DatabaseSession           _dbSession;
    private readonly PrescriptionRepository    _prescriptionRepo;
    private readonly HashSet<string>           _readNotificationKeys = new();
    private Action<Company>? _pendingCompanyReturn;
    private Action<Supplier>? _pendingSupplierReturn;
    private Action<Product>? _pendingProductReturn;

    public ChangePasswordViewModel ChangePasswordVM { get; }

    public MainWindowViewModel(
        DashboardViewModel        dashboardVM,
        ClinicalDashboardViewModel clinicalDashboardVM,
        PatientRegistryViewModel  patientVM,
        ProductRegistryViewModel productVM,
        PrescriptionViewModel     prescriptionVM,
        VisitHistoryViewModel     visitHistoryVM,
        UserRegistryViewModel     userVM,
        ReportsViewModel          reportsVM,
        ClinicalReportsViewModel  clinicalReportsVM,
        CompanyRegistryViewModel  companyVM,
        SupplierRegistryViewModel supplierVM,
        SearchViewModel           searchVM,
        SettingsViewModel         settingsVM,

        AppointmentViewModel      appointmentVM,
        PurchaseViewModel         purchaseVM,
        SaleViewModel             saleVM,
        InventoryViewModel        inventoryVM,
        ProfileViewModel          profileVM,
        ReturnsViewModel          returnsVM,
        ChangePasswordViewModel   changePasswordVM,
        DatabaseSession           dbSession,
        PrescriptionRepository     prescriptionRepo)
    {
        _dashboardVM    = dashboardVM;
        _clinicalDashboardVM = clinicalDashboardVM;
        _patientVM      = patientVM;
        _productVM     = productVM;
        _prescriptionVM = prescriptionVM;
        _visitHistoryVM = visitHistoryVM;
        _userVM         = userVM;
        _reportsVM      = reportsVM;
        _clinicalReportsVM = clinicalReportsVM;
        _companyVM      = companyVM;
        _supplierVM     = supplierVM;
        _searchVM       = searchVM;
        _settingsVM     = settingsVM;

        _appointmentVM  = appointmentVM;
        _purchaseVM     = purchaseVM;
        _saleVM         = saleVM;
        _inventoryVM    = inventoryVM;
        _profileVM      = profileVM;
        _returnsVM      = returnsVM;
        _dbSession      = dbSession;
        _prescriptionRepo = prescriptionRepo;

        WeakReferenceMessenger.Default.RegisterAll(this);

        ChangePasswordVM = changePasswordVM;
        ChangePasswordVM.CloseRequested += () => ShowChangePassword = false;

        _settingsVM.SettingsSaved += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DynamicClinicName = _settingsVM.ClinicName);
        };

        // Allow Dashboard to trigger the shared Change Password popup
        _dashboardVM.RequestChangePassword += () => OpenChangePasswordDialogCommand.Execute(null);

        // Allow Dashboard KPI cards to navigate to their respective pages
        _dashboardVM.RequestNavigatePatients     += () => ShowPatientsCommand.Execute(null);
        _dashboardVM.RequestNavigateCompanies    += () => ShowCompaniesCommand.Execute(null);
        _dashboardVM.RequestNavigateSuppliers    += () => ShowSuppliersCommand.Execute(null);
        _dashboardVM.RequestNavigateSales        += () => ShowSalesCommand.Execute(null);
        _dashboardVM.RequestNavigateAppointments += () => ShowAppointmentsCommand.Execute(null);
        _dashboardVM.RequestNavigateProducts    += () => ShowProductsCommand.Execute(null);
        _dashboardVM.RequestNavigateInventory    += () => ShowInventoryCommand.Execute(null);

        // Allow Patients view to navigate to Appointments
        _patientVM.RequestBookAppointment += (patient) => 
        {
            ShowAppointmentsCommand.Execute(null);
            _appointmentVM.PreselectPatient(patient);
        };

        _productVM.RequestAddCompany += () =>
        {
            _pendingCompanyReturn = async company => { NavigateTo(_productVM, "Products"); await _productVM.PreselectCompanyAsync(company.CompanyID); };
            NavigateTo(_companyVM, "Companies"); _companyVM.NewCommand.Execute(null);
        };
        _purchaseVM.RequestAddSupplier += () =>
        {
            _pendingSupplierReturn = async supplier => { NavigateTo(_purchaseVM, "Purchases"); await _purchaseVM.PreselectSupplierAsync(supplier.SupplierID); };
            NavigateTo(_supplierVM, "Suppliers"); _supplierVM.NewCommand.Execute(null);
        };
        _purchaseVM.RequestAddProduct += () =>
        {
            _pendingProductReturn = async product => { NavigateTo(_purchaseVM, "Purchases"); await _purchaseVM.PreselectProductAsync(product.ProductID); };
            NavigateTo(_productVM, "Products"); _productVM.NewCommand.Execute(null);
        };
        _saleVM.RequestAddProduct += () =>
        {
            _pendingProductReturn = async product => { NavigateTo(_saleVM, "Sales & Billing"); await _saleVM.PreselectProductAsync(product.ProductID); };
            NavigateTo(_productVM, "Products"); _productVM.NewCommand.Execute(null);
        };
        _companyVM.CompanySaved += company => { var callback=_pendingCompanyReturn; _pendingCompanyReturn=null; callback?.Invoke(company); };
        _supplierVM.SupplierSaved += supplier => { var callback=_pendingSupplierReturn; _pendingSupplierReturn=null; callback?.Invoke(supplier); };
        _productVM.ProductSaved += product => { var callback=_pendingProductReturn; _pendingProductReturn=null; callback?.Invoke(product); };

        CurrentSystemMode = ShouldStartInClinicalMode ? "Clinical" : "Pharma";

        // Start on the role's dashboard.
        if (CanAccessDashboard) NavigateTo(CurrentSystemMode == "Clinical" ? _clinicalDashboardVM : _dashboardVM, "Dashboard");
        else if (CanAccessPatients) NavigateTo(_patientVM, "Patients");
        else if (CanAccessAppointments) NavigateTo(_appointmentVM, "Appointments");
        else if (CanAccessPurchases) NavigateTo(_purchaseVM, "Purchases");
        else if (CanAccessSales) NavigateTo(_saleVM, "Sales & Billing");
        else if (CanAccessUsers) NavigateTo(_userVM, "Users");

        // Startup data load
        IsLoading = true;
        Task.Run(async () =>
        {
            try
            {
                await _patientVM.InitializeAsync();
                await _productVM.InitializeAsync();
                await _prescriptionVM.InitializeAsync();
                await _userVM.InitializeAsync();
                await _companyVM.InitializeAsync();
                await _supplierVM.InitializeAsync();
                await _appointmentVM.InitializeAsync();
                await _saleVM.InitializeAsync();
                await _purchaseVM.InitializeAsync();
                await _inventoryVM.InitializeAsync();
                await _returnsVM.InitializeAsync();
                await _settingsVM.InitializeAsync();
                await _dashboardVM.InitializeAsync();
                await _clinicalDashboardVM.InitializeAsync();
                await LoadNotificationsAsync();
                
                // Load active business name for the top bar
                var settings = await Task.Run(() => _dbSession.CreateConnection().QueryFirstOrDefault<string>(
                    "SELECT TOP 1 SettingValue FROM Settings WHERE SettingKey IN ('PharmacyName', 'ClinicName') ORDER BY CASE WHEN SettingKey = 'PharmacyName' THEN 0 ELSE 1 END") ?? "DR ASIF'S CLINIC");
                Avalonia.Threading.Dispatcher.UIThread.Post(() => DynamicClinicName = settings);

                // Compute alert warnings safely
                var lowStockCount = _productVM.Products.Count(m => m.IsLowStock && !m.IsExpired);
                var waitingTodayCount = _appointmentVM.Appointments.Count(a => a.AppointmentDate.Date == DateTime.Today && a.Status == "Checked-In");

                var alerts = new List<string>();
                if (waitingTodayCount > 0) alerts.Add($"{waitingTodayCount} patient(s) waiting today");
                if (lowStockCount > 0) alerts.Add($"{lowStockCount} low-stock product(s)");

                if (alerts.Count > 0)
                {
                    var msg = string.Join(" | ", alerts);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        AlertMessage = msg;
                        ShowAlert = true;
                    });
                }
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

        _syncTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _syncTimer.Tick += SyncTimer_Tick;
        _syncTimer.Start();
    }

    private Avalonia.Threading.DispatcherTimer? _syncTimer;

    private async void SyncTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentPageViewModel == null) return;
        
        if (CurrentPageViewModel is ProductRegistryViewModel productVM && productVM.Mode == ClinicSystem.UI.ViewModels.Products.FormMode.View)
        {
            await productVM.InitializeAsync();
        }
        else if (CurrentPageViewModel is CompanyRegistryViewModel companyVM && companyVM.Mode == ClinicSystem.UI.ViewModels.FormMode.View)
        {
            await companyVM.InitializeAsync();
        }
        else if (CurrentPageViewModel is SupplierRegistryViewModel supplierVM && supplierVM.Mode == ClinicSystem.UI.ViewModels.FormMode.View)
        {
            await supplierVM.InitializeAsync();
        }
        else if (CurrentPageViewModel is SaleViewModel saleVM && !saleVM.ShowForm)
        {
            await saleVM.InitializeAsync();
        }
        else if (CurrentPageViewModel is PurchaseViewModel purchaseVM && purchaseVM.Mode == ClinicSystem.UI.ViewModels.FormMode.View)
        {
            await purchaseVM.InitializeAsync();
        }
        else if (CurrentPageViewModel is InventoryViewModel inventoryVM)
        {
            await inventoryVM.InitializeAsync();
        }

        await LoadNotificationsAsync();
    }

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ViewModelBase? _currentPageViewModel;
    [ObservableProperty] private string _statusText   = string.Empty;
    [ObservableProperty] private string _pageTitle    = "Dashboard";
    [ObservableProperty] private string _dynamicClinicName = "DR ASIF'S CLINIC";
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _alertMessage = string.Empty;
    [ObservableProperty] private bool   _showAlert;
    [ObservableProperty] private string _currentSystemMode = "Pharma";

    public bool IsClinicalMode => CurrentSystemMode == "Clinical";
    public bool IsPharmaMode => CurrentSystemMode == "Pharma";
    // Allow switching if admin OR if user has access to modules in BOTH modes
    public bool CanSwitchSystemMode =>
        (CurrentUser?.IsAdmin ?? false) ||
        (HasAnyPharmaAccess() && HasAnyClinicalAccess());
    public string ProductNavigationLabel => IsClinicalMode ? "Drugs" : "Products";

    partial void OnCurrentSystemModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsClinicalMode));
        OnPropertyChanged(nameof(IsPharmaMode));
        OnPropertyChanged(nameof(ProductNavigationLabel));
        NotifyAccessChanged();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    public string SearchPlaceholder => (CurrentPageViewModel as ISearchable)?.SearchPlaceholder ?? "Search...";

    partial void OnSearchTextChanged(string value)
    {
        if (CurrentPageViewModel is ISearchable searchable)
        {
            searchable.SearchTerm = value;
        }
    }

    partial void OnCurrentPageViewModelChanged(ViewModelBase? value)
    {
        SearchText = string.Empty;
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    public string TodayDate        => DateTime.Now.ToString("dddd, MMM dd yyyy");
    public string CurrentUserName  => CurrentUser?.FullName.Length > 0 ? CurrentUser.FullName : CurrentUser?.Username ?? "Unknown";
    public string CurrentUserRole  => CurrentUser?.Role ?? string.Empty;
    public bool IsAdmin            => CurrentUser?.IsAdmin ?? false;

    // Module Access properties for UI binding
    // Dashboard access is mode-specific: admin/doctor always have it;
    // others need the relevant permission granted by the admin.
    public bool CanAccessDashboard =>
        CurrentUser?.IsAdmin == true || CurrentUser?.IsDoctor == true ||
        (IsClinicalMode && (CurrentUser?.HasAccess("Dashboard") ?? false)) ||
        (IsPharmaMode   && (CurrentUser?.HasAccess("PharmaDashboard") ?? false));
    private bool HasClinicalAccess(string module)
    {
        var user = CurrentUser;
        if (user?.IsAdmin == true || user?.IsDoctor == true) return true;
        if (user is null) return false;

        var roleCanUseClinicalModules =
            user.UserRole is ClinicSystem.Core.Enums.UserRole.Receptionist
                or ClinicSystem.Core.Enums.UserRole.Pharmacist
                or ClinicSystem.Core.Enums.UserRole.Assistant;
        return roleCanUseClinicalModules && user.HasAccess(module);
    }
    private bool ShouldStartInClinicalMode =>
        CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Doctor ||
        CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Assistant ||
        // For mixed-access users: start clinical only if they have clinical access
        // but have NO pharma access (otherwise start in pharma, or let them switch)
        (HasAnyClinicalAccess() && !HasAnyPharmaAccess());

    public bool CanAccessPatients     => IsClinicalMode && HasClinicalAccess("Patients");
    public bool CanAccessAppointments => IsClinicalMode && HasClinicalAccess("Appointments");
    public bool CanAccessProducts     => IsClinicalMode ? HasClinicalAccess("Products") : HasPharmaAccess("Products");
    public bool CanAccessCompanies    => IsPharmaMode && HasPharmaAccess("Companies");
    public bool CanAccessSuppliers    => IsPharmaMode && HasPharmaAccess("Suppliers");
    public bool CanAccessPurchases    => IsPharmaMode && HasPharmaAccess("Purchases");
    public bool CanAccessSales        => IsPharmaMode && HasPharmaAccess("Sales");
    public bool CanAccessInventory    => IsPharmaMode && HasPharmaAccess("Inventory");
    public bool CanAccessReports      => (IsClinicalMode && HasClinicalAccess("Reports")) || (IsPharmaMode && HasPharmaAccess("Reports"));
    public bool CanAccessReturns      => IsPharmaMode && HasPharmaAccess("Returns");
    public bool CanAccessUsers        => IsPharmaMode && (CurrentUser?.IsAdmin ?? false);
    public bool CanAccessSettings     => IsClinicalMode && HasClinicalAccess("Settings");

    private bool HasPharmaAccess(string module)
    {
        var user = CurrentUser;
        if (user?.IsAdmin == true) return true;
        if (user is null) return false;

        var roleCanUsePharmaModules =
            user.UserRole is ClinicSystem.Core.Enums.UserRole.Receptionist
                or ClinicSystem.Core.Enums.UserRole.Pharmacist
                or ClinicSystem.Core.Enums.UserRole.Assistant;
        return roleCanUsePharmaModules && user.HasAccess(module);
    }

    // Helper: does this user have access to ANY pharma module?
    private bool HasAnyPharmaAccess()
    {
        var user = CurrentUser;
        if (user?.IsAdmin == true) return true;
        if (user is null) return false;
        var roleOk = user.UserRole is ClinicSystem.Core.Enums.UserRole.Receptionist
            or ClinicSystem.Core.Enums.UserRole.Pharmacist
            or ClinicSystem.Core.Enums.UserRole.Assistant;
        if (!roleOk) return false;
        return user.HasAccess("Sales") || user.HasAccess("Purchases") || user.HasAccess("Inventory")
            || user.HasAccess("Products") || user.HasAccess("Companies") || user.HasAccess("Suppliers")
            || user.HasAccess("Returns") || user.HasAccess("Reports");
    }

    // Helper: does this user have access to ANY clinical module?
    private bool HasAnyClinicalAccess()
    {
        var user = CurrentUser;
        if (user?.IsAdmin == true || user?.IsDoctor == true) return true;
        if (user is null) return false;
        return user.HasAccess("Patients") || user.HasAccess("Appointments") || user.HasAccess("Settings");
    }

    // TRANSACTIONS
    public bool HasManagementAccess => CanAccessPatients || CanAccessAppointments || CanAccessProducts || CanAccessCompanies || CanAccessSuppliers || CanAccessSettings;
    public bool HasTransactionsAccess => CanAccessPurchases || CanAccessSales || CanAccessReturns;
    public bool HasAnalysisAccess => CanAccessInventory || CanAccessReports;
    public bool HasUserSettingsAccess => CanAccessUsers;

    // ── Active nav flags (for sidebar highlight) ───────────────────────────
    [ObservableProperty] private bool _isDashboardActive;
    [ObservableProperty] private bool _isPatientsActive;
    [ObservableProperty] private bool _isProductsActive;
    [ObservableProperty] private bool _isCompaniesActive;
    [ObservableProperty] private bool _isSuppliersActive;

    [ObservableProperty] private bool _isPurchasesActive;
    [ObservableProperty] private bool _isSalesActive;
    [ObservableProperty] private bool _isReturnsActive;
    [ObservableProperty] private bool _isInventoryActive;
    [ObservableProperty] private bool _isAppointmentsActive;
    [ObservableProperty] private bool _isUsersActive;
    [ObservableProperty] private bool _isReportsActive;
    [ObservableProperty] private bool _isSettingsActive;
    [ObservableProperty] private bool _isProfileActive;

    [ObservableProperty] private bool _showChangePassword;
    [ObservableProperty] private bool _showLogoutConfirm;
    [ObservableProperty] private ObservableCollection<AppNotification> _notifications = new();
    [ObservableProperty] private AppNotification? _selectedNotification;
    [ObservableProperty] private int _unreadNotificationCount;

    public bool HasNotifications => Notifications.Count > 0;
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;
    public bool CanSeeWorkflowNotifications => CurrentUser?.IsAdmin == true ||
        CurrentUser?.IsPharmacist == true ||
        CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Receptionist;

    private bool _visitHistoryLoaded;

    partial void OnSelectedNotificationChanged(AppNotification? value)
    {
        if (value == null) return;
        MarkNotificationRead(value.Key);
        
        if (value.Title == "New pharmacy handoff" && value.Payload is Prescription prescription)
        {
            ShowSalesCommand.Execute(null);
            _ = _saleVM.LoadFromHandoffAsync(prescription);
        }
    }

    public async void Receive(PrescriptionHandoffChangedMessage message) => await LoadNotificationsAsync();

    public void Receive(ClinicNameChangedMessage message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => DynamicClinicName = message.NewName);
    }

    private async Task LoadNotificationsAsync()
    {
        if (!CanSeeWorkflowNotifications)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Notifications = new ObservableCollection<AppNotification>();
                UnreadNotificationCount = 0;
                OnPropertyChanged(nameof(HasNotifications));
                OnPropertyChanged(nameof(HasUnreadNotifications));
            });
            return;
        }

        try
        {
            var handoffs = await Task.Run(() => _prescriptionRepo.GetPharmacyHandoffs(includeDispensed: true).ToList());
            var items = handoffs
                .Where(ShouldShowWorkflowNotification)
                .Select(ToNotification)
                .OrderByDescending(n => n.IsUnread)
                .ThenByDescending(n => ParseNotificationTime(n.TimeText))
                .ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Notifications = new ObservableCollection<AppNotification>(items);
                UnreadNotificationCount = items.Count(n => n.IsUnread);
                OnPropertyChanged(nameof(HasNotifications));
                OnPropertyChanged(nameof(HasUnreadNotifications));
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Notifications = new ObservableCollection<AppNotification>
                {
                    new()
                    {
                        Key = "notifications-load-error",
                        Title = "Notifications unavailable",
                        Message = ex.Message,
                        TimeText = DateTime.Now.ToString("h:mm tt"),
                        AccentBrushKey = "BrushRose",
                        IsUnread = true
                    }
                };
                UnreadNotificationCount = 1;
                OnPropertyChanged(nameof(HasNotifications));
                OnPropertyChanged(nameof(HasUnreadNotifications));
            });
        }
    }

    private bool ShouldShowWorkflowNotification(Prescription prescription)
    {
        if (CurrentUser?.IsAdmin == true) return true;
        if (CurrentUser?.IsPharmacist == true)
            return prescription.WorkflowStatus is "SentToPharmacy" or "Printed";
        if (CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Receptionist)
            return prescription.WorkflowStatus is "SentToPharmacy" or "Printed" or "Dispensed";
        return false;
    }

    private AppNotification ToNotification(Prescription prescription)
    {
        var patient = string.IsNullOrWhiteSpace(prescription.PatientName) ? "Patient" : prescription.PatientName;
        var status = prescription.WorkflowStatus;
        var key = $"prescription:{prescription.PrescriptionID}:{status}";
        var title = status switch
        {
            "SentToPharmacy" => "New pharmacy handoff",
            "Printed" => "Prescription ready to dispense",
            "Dispensed" => "Medicines dispensed",
            _ => "Prescription update"
        };
        var message = status switch
        {
            "SentToPharmacy" => $"{patient} was sent by {prescription.DoctorName ?? "doctor"} for pharmacy review.",
            "Printed" => $"{patient}'s prescription has been checked and printed.",
            "Dispensed" => $"{patient}'s medicines have been given.",
            _ => $"{patient}'s prescription status changed to {prescription.WorkflowStatusLabel}."
        };
        var time = prescription.DispensedAt ?? prescription.PrintedAt ?? prescription.SentToPharmacyAt ?? prescription.CreatedAt;

        return new AppNotification
        {
            Key = key,
            Title = title,
            Message = message,
            TimeText = time == default ? string.Empty : time.ToString("dd MMM, h:mm tt"),
            AccentBrushKey = status switch
            {
                "Printed" => "BrushAmber",
                "Dispensed" => "BrushEmerald",
                _ => "BrushPrimaryBlue"
            },
            IsUnread = !_readNotificationKeys.Contains(key),
            Payload = prescription
        };
    }

    private static DateTime ParseNotificationTime(string value) =>
        DateTime.TryParse(value, out var parsed) ? parsed : DateTime.MinValue;

    private void MarkNotificationRead(string key)
    {
        if (!_readNotificationKeys.Add(key)) return;
        Notifications = new ObservableCollection<AppNotification>(
            Notifications.Select(n => n.Key == key ? new AppNotification
            {
                Key = n.Key,
                Title = n.Title,
                Message = n.Message,
                TimeText = n.TimeText,
                AccentBrushKey = n.AccentBrushKey,
                IsUnread = false,
                Payload = n.Payload
            } : n));
        UnreadNotificationCount = Notifications.Count(n => n.IsUnread);
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasUnreadNotifications));
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        foreach (var notification in Notifications)
            _readNotificationKeys.Add(notification.Key);

        Notifications = new ObservableCollection<AppNotification>(
            Notifications.Select(n => new AppNotification
            {
                Key = n.Key,
                Title = n.Title,
                Message = n.Message,
                TimeText = n.TimeText,
                AccentBrushKey = n.AccentBrushKey,
                IsUnread = false,
                Payload = n.Payload
            }));
        UnreadNotificationCount = 0;
        OnPropertyChanged(nameof(HasUnreadNotifications));
    }

    // ── Navigation helper ──────────────────────────────────────────────────
    private void NavigateTo(ViewModelBase vm, string title)
    {
        CurrentPageViewModel = vm;
        PageTitle = title;
        ClearActiveFlags();
        
        IsDashboardActive     = vm is DashboardViewModel || vm is ClinicalDashboardViewModel;
        IsPatientsActive      = vm is PatientRegistryViewModel;
        IsProductsActive      = vm is ProductRegistryViewModel;
        IsCompaniesActive     = vm is CompanyRegistryViewModel;
        IsSuppliersActive     = vm is SupplierRegistryViewModel;
        IsPurchasesActive     = vm is PurchaseViewModel;
        IsSalesActive         = vm is SaleViewModel;
        IsReturnsActive       = vm is ReturnsViewModel;
        IsInventoryActive     = vm is InventoryViewModel;
        IsAppointmentsActive  = vm is AppointmentViewModel;
        IsUsersActive         = vm is UserRegistryViewModel;
        IsReportsActive       = vm is ReportsViewModel || vm is ClinicalReportsViewModel;
        IsSettingsActive      = vm is SettingsViewModel;
        IsProfileActive       = vm is ProfileViewModel;
    }

    private void ClearActiveFlags()
    {
        IsDashboardActive = IsPatientsActive = IsProductsActive =
        IsCompaniesActive = IsSuppliersActive = IsPurchasesActive = IsSalesActive = IsReturnsActive =
        IsInventoryActive = IsAppointmentsActive =
        IsUsersActive = IsReportsActive = IsSettingsActive = IsProfileActive = false;
    }

    // ── Navigation commands ────────────────────────────────────────────────
    [RelayCommand] private void ShowDashboard()
    {
        if (IsClinicalMode) { NavigateTo(_clinicalDashboardVM, "Dashboard"); _ = _clinicalDashboardVM.InitializeAsync(); }
        else { NavigateTo(_dashboardVM, "Dashboard"); _ = _dashboardVM.InitializeAsync(); }
    }
    [RelayCommand] private void ShowPatients()     { NavigateTo(_patientVM,      "Patients");     _ = _patientVM.InitializeAsync(); }
    [RelayCommand] private void ShowProducts()    { NavigateTo(_productVM, ProductNavigationLabel); _ = _productVM.InitializeAsync(); }
    [RelayCommand] private void ShowCompanies()    { NavigateTo(_companyVM,      "Companies");    _ = _companyVM.InitializeAsync(); }
    [RelayCommand] private void ShowSuppliers()    { NavigateTo(_supplierVM,     "Suppliers");    _ = _supplierVM.InitializeAsync(); }
 
    [RelayCommand] private void ShowPurchases()    { NavigateTo(_purchaseVM,     "Purchases");    _ = _purchaseVM.InitializeAsync(); }
    [RelayCommand] private void ShowSales()    { NavigateTo(_saleVM, "Sales & Billing"); _ = _saleVM.InitializeAsync(); }
    [RelayCommand] private void ShowReturns()  { NavigateTo(_returnsVM, "Returns"); _ = _returnsVM.InitializeAsync(); }
    [RelayCommand] private void ShowInventory() { NavigateTo(_inventoryVM, "Inventory"); _ = _inventoryVM.InitializeAsync(); }
    [RelayCommand] private void ShowAppointments() { NavigateTo(_appointmentVM, "Appointments"); _ = _appointmentVM.InitializeAsync(); }
    [RelayCommand] private void ShowUsers()        { NavigateTo(_userVM,         "Users");        _ = _userVM.InitializeAsync(); }
    [RelayCommand] private void ShowReports()      
    { 
        if (IsClinicalMode) { NavigateTo(_clinicalReportsVM, "Reports"); _ = _clinicalReportsVM.InitializeAsync(); }
        else { NavigateTo(_reportsVM, "Reports"); }
    }
    [RelayCommand]
    private void ShowSettings()
    {
        if (!IsClinicalMode) CurrentSystemMode = "Clinical";
        NavigateTo(_settingsVM, "Settings");
        _ = _settingsVM.InitializeAsync();
    }
    [RelayCommand] private void ShowProfile()
    {
        _profileVM.LoadFromCurrentUser();
        NavigateTo(_profileVM, "Profile");
    }

    [RelayCommand]
    private void SwitchToClinical()
    {
        if (!CanSwitchSystemMode) return;
        CurrentSystemMode = "Clinical";
        ShowDashboard();
    }

    [RelayCommand]
    private void SwitchToPharma()
    {
        if (!CanSwitchSystemMode) return;
        CurrentSystemMode = "Pharma";
        ShowDashboard();
    }

    private void NotifyAccessChanged()
    {
        foreach (var property in new[] { nameof(CanAccessDashboard), nameof(CanAccessPatients), nameof(CanAccessAppointments),
            nameof(CanAccessProducts), nameof(CanAccessCompanies), nameof(CanAccessSuppliers), nameof(CanAccessPurchases),
            nameof(CanAccessSales), nameof(CanAccessReturns), nameof(CanAccessInventory), nameof(CanAccessReports),
            nameof(CanAccessUsers), nameof(CanAccessSettings), nameof(HasManagementAccess), nameof(HasTransactionsAccess),
            nameof(HasAnalysisAccess), nameof(HasUserSettingsAccess) }) OnPropertyChanged(property);
    }

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
    [RelayCommand] private void Logout() => ShowLogoutConfirm = true;
    [RelayCommand] private void ConfirmLogout() { ShowLogoutConfirm = false; LogoutRequested?.Invoke(); }
    [RelayCommand] private void CancelLogout() => ShowLogoutConfirm = false;

    [RelayCommand]
    private void CloseAlert() => ShowAlert = false;


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
