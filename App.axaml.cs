
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClinicSystem.Data;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.Services;
using ClinicSystem.UI.ViewModels;
using ClinicSystem.UI.ViewModels.Products;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.ViewModels.Reports;
using ClinicSystem.UI.ViewModels.Companies;
using ClinicSystem.UI.ViewModels.Suppliers;
using ClinicSystem.UI.ViewModels.Appointments;
using ClinicSystem.UI.ViewModels.Purchases;
using ClinicSystem.UI.ViewModels.Sales;
using ClinicSystem.UI.ViewModels.Inventory;
using ClinicSystem.UI.ViewModels.Dashboard;
using ClinicSystem.UI.ViewModels.Users;
using ClinicSystem.UI.ViewModels.Search;
using ClinicSystem.UI.ViewModels.Settings;
using ClinicSystem.UI.ViewModels.Profile;
using ClinicSystem.UI.ViewModels.Returns;
using ClinicSystem.UI.Views;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicSystem.UI;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Read from bin/output directory (where the app actually runs from)
            string? cs = ConnectionStringHelper.ReadEffective();

            if (ConnectionStringHelper.IsPlaceholder(cs))
            {
                // No valid connection string — show setup screen
                ShowDbSetupWindow(desktop, previousWindow: null,
                    initialError: "No database connection has been configured for this computer.");
            }
            else
            {
                // Try to connect with the stored string
                var (ok, error) = ConnectionStringHelper.TestConnection(cs!);
                if (!ok)
                {
                    ShowDbSetupWindow(desktop, previousWindow: null,
                        initialError: $"Could not connect to database: {error}");
                }
                else
                {
                    // All good — boot normally
                    BuildServicesAndLogin(desktop, previousWindow: null);
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── DB Setup Window ───────────────────────────────────────────────────────
    private void ShowDbSetupWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window? previousWindow,
        string initialError = "")
    {
        var setupVM = new DbSetupViewModel
        {
            InitialError    = initialError,
            HasInitialError = !string.IsNullOrEmpty(initialError)
        };

        // Pre-populate fields from any existing connection string
        var existing = ConnectionStringHelper.ReadEffective();
        if (!ConnectionStringHelper.IsPlaceholder(existing))
            setupVM.PrePopulateFromExisting(existing);

        var setupWindow = new DbSetupWindow { DataContext = setupVM };

        setupVM.SetupCompleted += (_) =>
        {
            // SaveAndContinueAsync already wrote the new string to appsettings.local.json.
            // Just rebuild DI (reads from files) and proceed to login.
            BuildServicesAndLogin(desktop, setupWindow);
        };

        desktop.MainWindow = setupWindow;
        setupWindow.Show();
        previousWindow?.Close();
    }

    // ── Build DI + show Login ─────────────────────────────────────────────────
    /// <summary>
    /// Builds the DI container from appsettings files and shows the Login window.
    /// appsettings.local.json (if present) overrides appsettings.json — same as original.
    /// This method is called AFTER the connection string has been verified or saved.
    /// </summary>
    private void BuildServicesAndLogin(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window? previousWindow)
    {
        // Standard configuration — exactly the same as the original working code.
        // appsettings.local.json is written by ConnectionStringHelper.SaveToLocalJson()
        // before this is called, so it's always up-to-date.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json",  optional: true,  reloadOnChange: false)
            .Build();

        // Build DI container
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<DatabaseSession>();

        // Repositories
        services.AddSingleton<PatientRepository>();
        services.AddSingleton<ProductRepository>();
        services.AddSingleton<UserRepository>();
        services.AddSingleton<PrescriptionRepository>();
        services.AddSingleton<CompanyRepository>();
        services.AddSingleton<SupplierRepository>();
        services.AddSingleton<AppointmentRepository>();
        services.AddSingleton<PurchaseRepository>();
        services.AddSingleton<SaleRepository>();
        services.AddSingleton<ReturnRepository>();
        services.AddSingleton<DiscountRefundRepository>();
        services.AddSingleton<ActivityLogRepository>();
        services.AddSingleton<SettingsRepository>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<PatientRegistryViewModel>();
        services.AddSingleton<ProductRegistryViewModel>();
        services.AddSingleton<PrescriptionViewModel>();
        services.AddSingleton<VisitHistoryViewModel>();
        services.AddSingleton<UserRegistryViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<CompanyRegistryViewModel>();
        services.AddSingleton<SupplierRegistryViewModel>();
        services.AddSingleton<AppointmentViewModel>();
        services.AddSingleton<PurchaseViewModel>();
        services.AddSingleton<SaleViewModel>();
        services.AddSingleton<InvoiceViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ChangePasswordViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DiscountRefundViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<ReturnsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        _services = services.BuildServiceProvider();

        // Initialize singletons
        var activityRepo = _services.GetRequiredService<ActivityLogRepository>();
        ClinicSystem.Data.Services.ActivityService.Initialize(activityRepo);
        ClinicSystem.Data.Services.ActivityService.OnActivityLogged += log =>
            WeakReferenceMessenger.Default.Send(new ActivityLogMessage(log));

        var settingsRepo = _services.GetRequiredService<SettingsRepository>();
        ClinicSystem.UI.Services.ThemeService.Initialize(settingsRepo);

        ShowLoginWindow(desktop, previousWindow);
    }

    // ── Login Window ──────────────────────────────────────────────────────────
    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop, Window? previousWindow)
    {
        if (_services is null) return;

        var loginVM     = _services.GetRequiredService<LoginViewModel>();
        var loginWindow = new LoginWindow { DataContext = loginVM };

        loginVM.LoginSucceeded += _ => ShowMainWindow(desktop, loginWindow);

        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        previousWindow?.Close();
    }

    // ── Main Window ───────────────────────────────────────────────────────────
    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window? previousWindow)
    {
        if (_services is null) return;

        var mainVM     = _services.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow { DataContext = mainVM };

        mainVM.LogoutRequested += () =>
        {
            ViewModelBase.CurrentUser = null;
            ShowLoginWindow(desktop, mainWindow);
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        previousWindow?.Close();
    }
}
