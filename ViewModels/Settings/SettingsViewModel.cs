using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Data;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.ViewModels.Users;
using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Globalization;
using System.Collections.Generic;

namespace ClinicSystem.UI.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly DatabaseSession _dbSession;
    private readonly SettingsRepository _repo;
    public ChangePasswordViewModel ChangePasswordVM { get; }

    public SettingsViewModel(DatabaseSession dbSession, SettingsRepository repo, ChangePasswordViewModel changePasswordVM)
    {
        _dbSession = dbSession;
        _repo = repo;
        ChangePasswordVM = changePasswordVM;
        ChangePasswordVM.CloseRequested += () => IsChangePasswordVisible = false;
    }

    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    [ObservableProperty] private bool _isChangePasswordVisible;

    [RelayCommand]
    private void OpenChangePassword()
    {
        ChangePasswordVM.Reset();
        IsChangePasswordVisible = true;
    }

    // -- General Settings
    [ObservableProperty] private string _clinicName = "Care & Cure Clinic";
    [ObservableProperty] private string _clinicAddress = "123 Health Ave, Medical District";
    [ObservableProperty] private string _clinicPhone = "+92 300 1234567";
    [ObservableProperty] private string _clinicEmail = "info@careandcure.com";

    // -- Billing Settings
    [ObservableProperty] private string _invoicePrefix = "INV-";
    [ObservableProperty] private decimal _defaultTaxRate = 0m;
    [ObservableProperty] private string _currency = "PKR";

    // -- Inventory Settings
    [ObservableProperty] private int _lowStockThreshold = 10;
    [ObservableProperty] private int _expiryAlertDays = 30;

    // -- System Settings
    [ObservableProperty] private string _dateFormat = "yyyy-MM-dd";
    [ObservableProperty] private string _timeFormat = "HH:mm";
    [ObservableProperty] private string _language = "English";

    // Selectable options
    public List<string> DateFormatOptions { get; } = new() { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };
    public List<string> TimeFormatOptions { get; } = new() { "HH:mm", "hh:mm tt" };
    public List<string> LanguageOptions { get; } = new() { "English", "Urdu", "Arabic" };

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var dict = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (dict.TryGetValue("ClinicName", out var val)) ClinicName = val;
                if (dict.TryGetValue("ClinicAddress", out val)) ClinicAddress = val;
                if (dict.TryGetValue("ClinicPhone", out val)) ClinicPhone = val;
                if (dict.TryGetValue("ClinicEmail", out val)) ClinicEmail = val;
                
                if (dict.TryGetValue("InvoicePrefix", out val)) InvoicePrefix = val;
                if (dict.TryGetValue("DefaultTaxRate", out val) && decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var t)) DefaultTaxRate = t;
                if (dict.TryGetValue("Currency", out val)) Currency = val;
                
                if (dict.TryGetValue("LowStockThreshold", out val) && int.TryParse(val, out var ls)) LowStockThreshold = ls;
                if (dict.TryGetValue("ExpiryAlertDays", out val) && int.TryParse(val, out var ed)) ExpiryAlertDays = ed;
                
                if (dict.TryGetValue("DateFormat", out val)) DateFormat = val;
                if (dict.TryGetValue("TimeFormat", out val)) TimeFormat = val;
                if (dict.TryGetValue("Language", out val)) Language = val;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load settings: {ex.Message}");
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                _repo.SetValue("ClinicName", ClinicName);
                _repo.SetValue("ClinicAddress", ClinicAddress);
                _repo.SetValue("ClinicPhone", ClinicPhone);
                _repo.SetValue("ClinicEmail", ClinicEmail);
                
                _repo.SetValue("InvoicePrefix", InvoicePrefix);
                _repo.SetValue("DefaultTaxRate", DefaultTaxRate.ToString(CultureInfo.InvariantCulture));
                _repo.SetValue("Currency", Currency);
                
                _repo.SetValue("LowStockThreshold", LowStockThreshold.ToString());
                _repo.SetValue("ExpiryAlertDays", ExpiryAlertDays.ToString());
                
                _repo.SetValue("DateFormat", DateFormat);
                _repo.SetValue("TimeFormat", TimeFormat);
                _repo.SetValue("Language", Language);
            });
            StatusMessage = "Settings saved successfully!";
            LogActivity("Settings Updated", "Application settings were updated", "Settings");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Backup & Restore ──────────────────────────────────────────────────
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

    // ── Reset & Rollback (Database Maintenance) ─────────────────────────────
    [ObservableProperty] private bool _isResetConfirmVisible;
    [ObservableProperty] private bool _isRollbackAvailable;

    [RelayCommand]
    private void ShowResetConfirm()
    {
        IsResetConfirmVisible = true;
        StatusMessage = "⚠️ Warning: This will permanently delete ALL patient, product, and company data. A rollback backup will be created automatically.";
    }

    [RelayCommand]
    private void CancelReset()
    {
        IsResetConfirmVisible = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmResetAsync()
    {
        IsBusy = true;
        IsResetConfirmVisible = false;
        StatusMessage = "Resetting all data… creating rollback backup first…";
        try
        {
            await Task.Run(() => _dbSession.ResetAllData());
            IsRollbackAvailable = true;
            StatusMessage = "✅ All data has been reset to zero. A rollback backup was saved automatically — click 'Rollback Reset' to undo.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RollbackResetAsync()
    {
        IsBusy = true;
        StatusMessage = "Rolling back — restoring pre-reset backup…";
        try
        {
            await Task.Run(() => _dbSession.RollbackReset());
            IsRollbackAvailable = false;
            StatusMessage = "✅ Data restored successfully! Please restart the application to see the recovered data.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rollback failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
