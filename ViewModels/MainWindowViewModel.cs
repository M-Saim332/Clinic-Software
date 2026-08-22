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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Core.Models;


namespace ClinicSystem.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
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
        DatabaseSession           dbSession)
    {
        _dashboardVM    = dashboardVM;
        _clinicalDashboardVM = clinicalDashboardVM;
        _patientVM      = patientVM;
        _productVM     = productVM;
        _prescriptionVM = prescriptionVM;
        _visitHistoryVM = visitHistoryVM;
        _userVM         = userVM;
        _reportsVM      = reportsVM;
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

        ChangePasswordVM = changePasswordVM;
        ChangePasswordVM.CloseRequested += () => ShowChangePassword = false;

        _settingsVM.SettingsSaved += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ClinicName = _settingsVM.ClinicName);
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

        CurrentSystemMode = CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Doctor ? "Clinical" : "Pharma";

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
                
                // Load Clinic Name for the top bar
                var settings = await Task.Run(() => _dbSession.CreateConnection().QueryFirstOrDefault<string>("SELECT SettingValue FROM Settings WHERE SettingKey = 'ClinicName'") ?? "Care & Cure Clinic");
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ClinicName = settings);

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
    }

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ViewModelBase? _currentPageViewModel;
    [ObservableProperty] private string _statusText   = string.Empty;
    [ObservableProperty] private string _pageTitle    = "Dashboard";
    [ObservableProperty] private string _clinicName   = "Care & Cure Clinic";
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _alertMessage = string.Empty;
    [ObservableProperty] private bool   _showAlert;
    [ObservableProperty] private string _currentSystemMode = "Pharma";

    public bool IsClinicalMode => CurrentSystemMode == "Clinical";
    public bool IsPharmaMode => CurrentSystemMode == "Pharma";
    public bool CanSwitchSystemMode => CurrentUser?.IsAdmin ?? false;
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
    public bool CanAccessDashboard    => true;
    public bool CanAccessPatients     => IsClinicalMode && (CurrentUser?.IsDoctor ?? false);
    public bool CanAccessAppointments => IsClinicalMode && (CurrentUser?.IsDoctor ?? false);
    public bool CanAccessProducts     => IsClinicalMode ? (CurrentUser?.IsDoctor ?? false) : HasPharmaAccess("Products");
    public bool CanAccessCompanies    => IsPharmaMode && HasPharmaAccess("Companies");
    public bool CanAccessSuppliers    => IsPharmaMode && HasPharmaAccess("Suppliers");
    public bool CanAccessPurchases    => IsPharmaMode && HasPharmaAccess("Purchases");
    public bool CanAccessSales        => IsPharmaMode && HasPharmaAccess("Sales");
    public bool CanAccessInventory    => IsPharmaMode && HasPharmaAccess("Inventory");
    public bool CanAccessReports      => IsPharmaMode && HasPharmaAccess("Reports");
    public bool CanAccessReturns      => IsPharmaMode && HasPharmaAccess("Returns");
    public bool CanAccessUsers        => IsPharmaMode && (CurrentUser?.IsAdmin ?? false);
    public bool CanAccessSettings     => IsPharmaMode && HasPharmaAccess("Settings");

    private bool HasPharmaAccess(string module) => CurrentUser?.IsAdmin == true ||
        ((CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Receptionist || CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Pharmacist) && CurrentUser.HasAccess(module));

    // Sidebar Category Visibilities — new order: Dashboard → Transactions → Management → Analysis
    public bool HasManagementAccess => CanAccessPatients || CanAccessAppointments || CanAccessProducts || CanAccessCompanies || CanAccessSuppliers;
    public bool HasTransactionsAccess => CanAccessPurchases || CanAccessSales || CanAccessReturns;
    public bool HasAnalysisAccess => CanAccessInventory || CanAccessReports;
    public bool HasUserSettingsAccess => CanAccessUsers || CanAccessSettings;

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
            case "Products":    IsProductsActive    = true; break;
            case "Drugs":       IsProductsActive    = true; break;
            case "Companies":    IsCompaniesActive    = true; break;
            case "Suppliers":    IsSuppliersActive    = true; break;

            case "Purchases":    IsPurchasesActive    = true; break;
            case "Sales & Billing": IsSalesActive     = true; break;
            case "Returns":      IsReturnsActive   = true; break;
            case "Inventory":    IsInventoryActive    = true; break;
            case "Appointments": IsAppointmentsActive = true; break;
            case "Users":        IsUsersActive        = true; break;
            case "Reports":      IsReportsActive      = true; break;
            case "Settings":     IsSettingsActive     = true; break;
            case "Profile":      IsProfileActive      = true; break;
        }
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
    [RelayCommand] private void ShowSales()        { NavigateTo(_saleVM,         "Sales & Billing"); _ = _saleVM.InitializeAsync(); }
    [RelayCommand] private void ShowReturns()      { NavigateTo(_returnsVM,      "Returns"); _ = _returnsVM.InitializeAsync(); }
    [RelayCommand] private void ShowInventory()    { NavigateTo(_inventoryVM,    "Inventory");    _ = _inventoryVM.InitializeAsync(); }
    [RelayCommand] private void ShowAppointments() { NavigateTo(_appointmentVM, "Appointments"); _ = _appointmentVM.InitializeAsync(); }
    [RelayCommand] private void ShowUsers()        { NavigateTo(_userVM,         "Users");        _ = _userVM.InitializeAsync(); }
    [RelayCommand] private void ShowReports()      { NavigateTo(_reportsVM,      "Reports"); }
    [RelayCommand] private void ShowSettings()     { NavigateTo(_settingsVM,     "Settings"); _ = _settingsVM.InitializeAsync(); }
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
