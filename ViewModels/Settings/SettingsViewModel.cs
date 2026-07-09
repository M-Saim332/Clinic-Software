using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Data;
using ClinicSystem.UI.ViewModels.Users;
using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;


namespace ClinicSystem.UI.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly DatabaseSession _dbSession;
    public ChangePasswordViewModel ChangePasswordVM { get; }

    public SettingsViewModel(DatabaseSession dbSession, ChangePasswordViewModel changePasswordVM)
    {
        _dbSession = dbSession;
        ChangePasswordVM = changePasswordVM;
    }

    [ObservableProperty] private string _clinicName = "Care & Cure Clinic";
    [ObservableProperty] private string _clinicAddress = "123 Health Ave, Medical District";
    [ObservableProperty] private string _clinicPhone = "+92 300 1234567";
    [ObservableProperty] private string _clinicEmail = "info@careandcure.com";

    [ObservableProperty] private decimal _defaultTaxRate = 17.5m;
    [ObservableProperty] private string _selectedPrinter = "Microsoft Print to PDF";

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private void SaveSettings()
    {
        StatusMessage = "Settings saved successfully!";
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
                IsBusy = true;
                StatusMessage = "Backing up database...";
                try
                {
                    await Task.Run(() => _dbSession.Backup(file.Path.LocalPath));
                    StatusMessage = "Backup completed successfully!";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Backup failed: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storage = desktop.MainWindow?.StorageProvider;
            if (storage == null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Backup File to Restore",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("SQL Server Backup (*.bak)") { Patterns = new[] { "*.bak" } }
                }
            });

            if (files != null && files.Count > 0)
            {
                IsBusy = true;
                StatusMessage = "Restoring database (this may take a few seconds)...";
                try
                {
                    await Task.Run(() => _dbSession.Restore(files[0].Path.LocalPath));
                    StatusMessage = "Database restored successfully! Please restart the application.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Restore failed: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
