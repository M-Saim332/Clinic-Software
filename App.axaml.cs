using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClinicSystem.Data;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.ViewModels;
using ClinicSystem.UI.ViewModels.Medicines;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.ViewModels.Reports;
using ClinicSystem.UI.ViewModels.Users;
using ClinicSystem.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicSystem.UI;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Build configuration
        // appsettings.local.json is gitignored and machine-specific (overrides the base file).
        // Each developer creates their own local file with their connection string.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .Build();

        // Build DI container
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<DatabaseSession>();

        // Repositories
        services.AddSingleton<PatientRepository>();
        services.AddSingleton<MedicineRepository>();
        services.AddSingleton<UserRepository>();
        services.AddSingleton<PrescriptionRepository>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<PatientRegistryViewModel>();
        services.AddSingleton<MedicineRegistryViewModel>();
        services.AddSingleton<PrescriptionViewModel>();
        services.AddSingleton<VisitHistoryViewModel>();
        services.AddSingleton<UserRegistryViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShowLoginWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_services is null) return;

        var previousWindow = desktop.MainWindow;
        var loginVM = _services.GetRequiredService<LoginViewModel>();
        var loginWindow = new LoginWindow { DataContext = loginVM };

        loginVM.LoginSucceeded += _ => ShowMainWindow(desktop, loginWindow);

        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        previousWindow?.Close();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window? previousWindow)
    {
        if (_services is null) return;

        var mainVM = _services.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow { DataContext = mainVM };

        mainVM.LogoutRequested += () =>
        {
            ViewModelBase.CurrentUser = null;
            ShowLoginWindow(desktop);
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        previousWindow?.Close();
    }
}
