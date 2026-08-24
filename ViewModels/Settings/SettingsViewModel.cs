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
using System.Linq;
using ClinicSystem.UI.Services;

namespace ClinicSystem.UI.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase, ISearchable
{
    private readonly DatabaseSession    _dbSession;
    private readonly SettingsRepository _repo;
    public ChangePasswordViewModel ChangePasswordVM { get; }

    public UserRegistryViewModel UserRegistryVM { get; }

    public SettingsViewModel(
        DatabaseSession dbSession,
        SettingsRepository repo,
        ChangePasswordViewModel changePasswordVM,
        UserRegistryViewModel userRegistryVM)
    {
        _dbSession       = dbSession;
        _repo            = repo;
        ChangePasswordVM = changePasswordVM;
        UserRegistryVM   = userRegistryVM;
        ChangePasswordVM.CloseRequested += () => IsChangePasswordVisible = false;
    }

    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    [ObservableProperty] private bool _isChangePasswordVisible;
    [ObservableProperty] private int _selectedCategoryIndex = 0;

    [RelayCommand]
    private void OpenChangePassword()
    {
        ChangePasswordVM.Reset();
        IsChangePasswordVisible = true;
    }

    // ── General Settings ─────────────────────────────────────────────────────
    [ObservableProperty] private string _clinicName    = "Care & Cure Clinic";
    [ObservableProperty] private string _pharmacyName = "My Pharmacy";
    [ObservableProperty] private string _receptionistTheme = "System Default";
    public IEnumerable<string> ReceptionistThemeOptions => ThemeService.AllThemes.Select(t => t.Name);
    [ObservableProperty] private string _clinicAddress = "123 Health Ave, Medical District";
    [ObservableProperty] private string _clinicPhone   = "+92 300 1234567";
    [ObservableProperty] private string _clinicEmail   = "info@careandcure.com";

    // ── Billing Settings ─────────────────────────────────────────────────────
    [ObservableProperty] private string  _invoicePrefix  = "INV-";
    [ObservableProperty] private string  _currency       = "PKR";

    // ── Inventory Settings ───────────────────────────────────────────────────
    [ObservableProperty] private int _expiryAlertDays   = 30;

    // ── System Settings ──────────────────────────────────────────────────────
    [ObservableProperty] private string _dateFormat = "yyyy-MM-dd";
    [ObservableProperty] private string _timeFormat = "HH:mm";
    [ObservableProperty] private string _language   = "English";

    public List<string> DateFormatOptions { get; } = new() { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };
    public List<string> TimeFormatOptions { get; } = new() { "HH:mm", "hh:mm tt" };
    public List<string> LanguageOptions   { get; } = new() { "English", "Urdu", "Arabic" };

    // ── Appearance / Theme ───────────────────────────────────────────────────
    [ObservableProperty]
    private string _selectedTheme = ThemeService.CurrentThemeName;

    /// <summary>All built-in themes.</summary>
    public IEnumerable<ThemeDefinition> BuiltInThemesList =>
        ThemeService.BuiltInThemes;

    /// <summary>Admin-created custom themes.</summary>
    public IList<ThemeDefinition> CustomThemesList =>
        ThemeService.CustomThemes;

    partial void OnSelectedThemeChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            ThemeService.ApplyTheme(value);
    }

    // ── Custom Theme Builder ─────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomBuilderPreviewSidebar))]
    [NotifyPropertyChangedFor(nameof(CustomBuilderPreviewPrimary))]
    private string _newThemeName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomBuilderPreviewSidebar))]
    private string _newThemeSidebarHex = "#1E293B";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomBuilderPreviewPrimary))]
    private string _newThemePrimaryHex = "#4F46E5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomBuilderPreviewTopBar))]
    private string _newThemeTopBarHex = "#FFFFFF";

    [ObservableProperty] private bool _newThemeIsDark;

    /// <summary>Live preview swatch — sidebar hex (validated).</summary>
    public string CustomBuilderPreviewSidebar =>
        IsValidHex(NewThemeSidebarHex) ? NewThemeSidebarHex : "#1E293B";

    /// <summary>Live preview swatch — primary hex (validated).</summary>
    public string CustomBuilderPreviewPrimary =>
        IsValidHex(NewThemePrimaryHex) ? NewThemePrimaryHex : "#4F46E5";

    /// <summary>Live preview swatch — topbar hex (validated).</summary>
    public string CustomBuilderPreviewTopBar =>
        IsValidHex(NewThemeTopBarHex) ? NewThemeTopBarHex : "#FFFFFF";

    [ObservableProperty] private string _customBuilderMessage = string.Empty;

    [RelayCommand]
    private void CreateCustomTheme()
    {
        var name = NewThemeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CustomBuilderMessage = "⚠ Please enter a theme name.";
            return;
        }
        if (!IsValidHex(NewThemeSidebarHex) || !IsValidHex(NewThemePrimaryHex) || !IsValidHex(NewThemeTopBarHex))
        {
            CustomBuilderMessage = "⚠ Enter valid hex colors (e.g. #1E293B).";
            return;
        }
        if (ThemeService.AllThemes.Any(t => t.Name == name))
        {
            CustomBuilderMessage = $"⚠ A theme named \"{name}\" already exists.";
            return;
        }

        // Derive companion colors automatically
        bool dark = NewThemeIsDark;
        string primary  = NewThemePrimaryHex;
        string sidebar  = NewThemeSidebarHex;
        string topbar   = NewThemeTopBarHex;
        string hover    = LightenOrDarken(sidebar, dark ? 0.08 : 0.05);
        string content  = dark ? "#111827" : "#F8FAFC";
        string card     = dark ? "#1F2937" : "#FFFFFF";
        string fg       = dark ? "#F9FAFB" : "#111827";
        string subtle   = dark ? "#9CA3AF" : "#6B7280";
        string border   = dark ? "#374151" : "#E5E7EB";
        string input    = dark ? "#1F2937" : "#FFFFFF";

        var custom = new ThemeDefinition
        {
            Name                   = name,
            IsDark                 = dark,
            IsCustom               = true,
            SidebarBackground      = sidebar,
            SidebarForeground      = dark ? "#D1D5DB" : "#CBD5E1",
            SidebarHoverBackground = hover,
            SidebarHoverForeground = fg,
            SidebarActiveBackground= primary,
            SidebarActiveForeground= "#FFFFFF",
            TopBarBackground       = topbar,
            TopBarForeground       = dark ? "#D1D5DB" : "#4B5563",
            Primary                = primary,
            PrimaryHover           = LightenOrDarken(primary, -0.1),
            PrimaryPressed         = LightenOrDarken(primary, -0.2),
            PrimaryLight           = dark ? LightenOrDarken(primary, -0.5) : LightenOrDarken(primary, 0.35),
            ContentBackground      = content,
            CardBackground         = card,
            ContentForeground      = fg,
            SubtleForeground       = subtle,
            BorderColor            = border,
            InputBackground        = input
        };

        ThemeService.SaveCustomTheme(custom);
        OnPropertyChanged(nameof(CustomThemesList));
        SelectedTheme = name;
        CustomBuilderMessage = $"✓ Theme \"{name}\" created and applied.";
        NewThemeName = string.Empty;
    }

    [RelayCommand]
    private void DeleteCustomTheme(string themeName)
    {
        ThemeService.DeleteCustomTheme(themeName);
        OnPropertyChanged(nameof(CustomThemesList));
        if (SelectedTheme == themeName)
        {
            SelectedTheme = "Slate & Indigo";
        }
        CustomBuilderMessage = $"Theme \"{themeName}\" deleted.";
    }

    // ── Status / Search ──────────────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isBusy;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Settings...";

    partial void OnSearchTermChanged(string value) { /* future */ }

    // ── Initialization ───────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var dict = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (dict.TryGetValue("ClinicName",    out var val)) ClinicName    = val;
                if (dict.TryGetValue("PharmacyName",  out val)) PharmacyName = val;
                if (dict.TryGetValue("ReceptionistTheme", out val)) ReceptionistTheme = val;
                if (dict.TryGetValue("ClinicAddress", out val))     ClinicAddress = val;
                if (dict.TryGetValue("ClinicPhone",   out val))     ClinicPhone   = val;
                if (dict.TryGetValue("ClinicEmail",   out val))     ClinicEmail   = val;

                if (dict.TryGetValue("InvoicePrefix",  out val)) InvoicePrefix  = val;
                if (dict.TryGetValue("Currency", out val)) Currency = val;

                if (dict.TryGetValue("ExpiryAlertDays",   out val) && int.TryParse(val, out var ed)) ExpiryAlertDays   = ed;

                if (dict.TryGetValue("DateFormat", out val)) DateFormat = val;
                if (dict.TryGetValue("TimeFormat", out val)) TimeFormat = val;
                if (dict.TryGetValue("Language",   out val)) Language   = val;

                if (dict.TryGetValue("AppTheme", out val) &&
                    ThemeService.AllThemes.Any(t2 => t2.Name == val))
                {
                    SelectedTheme = val;
                }

                // Refresh custom themes list (loaded by ThemeService at startup)
                OnPropertyChanged(nameof(CustomThemesList));
                OnPropertyChanged(nameof(BuiltInThemesList));
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load settings: {ex.Message}");
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsBusy = false);
        }
    }

    public event Action? SettingsSaved;

    // ── Save ─────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!string.IsNullOrWhiteSpace(ClinicPhone) && !ClinicSystem.UI.Helpers.ValidationHelper.IsValidPhone(ClinicPhone))
        {
            StatusMessage = "Phone number must contain exactly 11 digits.";
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                _repo.SetValue("ClinicName",    ClinicName);
                _repo.SetValue("PharmacyName",  PharmacyName);
                _repo.SetValue("ReceptionistTheme", ReceptionistTheme);
                _repo.SetValue("ClinicAddress", ClinicAddress);
                _repo.SetValue("ClinicPhone",   ClinicPhone);
                _repo.SetValue("ClinicEmail",   ClinicEmail);

                _repo.SetValue("InvoicePrefix",  InvoicePrefix);
                _repo.SetValue("Currency",       Currency);

                _repo.SetValue("ExpiryAlertDays",   ExpiryAlertDays.ToString());

                _repo.SetValue("DateFormat", DateFormat);
                _repo.SetValue("TimeFormat", TimeFormat);
                _repo.SetValue("Language",   Language);

                _repo.SetValue("AppTheme", SelectedTheme ?? "Slate & Indigo");
            });
            StatusMessage = "Settings saved successfully!";
            LogActivity("Settings Updated", "Application settings were updated", "Settings");
            SettingsSaved?.Invoke();
        }
        catch (Exception ex) { StatusMessage = $"Failed to save settings: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ── Backup &amp; Restore ──────────────────────────────────────────────────
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
                DefaultExtension  = "bak",
                FileTypeChoices   = new[]
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
                catch (Exception ex) { StatusMessage = $"Backup failed: {ex.Message}"; }
                finally { IsBusy = false; }
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
                Title          = "Select Backup File to Restore",
                AllowMultiple  = false,
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
                catch (Exception ex) { StatusMessage = $"Restore failed: {ex.Message}"; }
                finally { IsBusy = false; }
            }
        }
    }

    // ── Reset &amp; Rollback ──────────────────────────────────────────────────
    [ObservableProperty] private bool _isResetConfirmVisible;
    [ObservableProperty] private bool _isRollbackAvailable;

    [RelayCommand] private void ShowResetConfirm()
    {
        IsResetConfirmVisible = true;
        StatusMessage = "⚠️ Warning: This will permanently delete ALL patient, product, and company data.";
    }

    [RelayCommand] private void CancelReset()
    {
        IsResetConfirmVisible = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmResetAsync()
    {
        IsBusy = true;
        IsResetConfirmVisible = false;
        StatusMessage = "Deleting all data… creating rollback backup first…";
        try
        {
            await Task.Run(() => _dbSession.ResetAllData());
            IsRollbackAvailable = true;
            StatusMessage = "✅ All data deleted. Click 'Rollback Reset' to undo.";
        }
        catch (Exception ex) { StatusMessage = $"Deletion failed: {ex.Message}"; }
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
            StatusMessage = "✅ Data restored successfully! Please restart the application.";
        }
        catch (Exception ex) { StatusMessage = $"Rollback failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static bool IsValidHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        return Avalonia.Media.Color.TryParse(hex, out _);
    }

    /// <summary>Crude lightening/darkening by blending toward white or black.</summary>
    private static string LightenOrDarken(string hex, double amount)
    {
        if (!Avalonia.Media.Color.TryParse(hex, out var c)) return hex;
        if (amount >= 0)
        {
            byte r = (byte)Math.Min(255, c.R + (int)(amount * 255));
            byte g = (byte)Math.Min(255, c.G + (int)(amount * 255));
            byte b = (byte)Math.Min(255, c.B + (int)(amount * 255));
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        else
        {
            double f = 1 + amount;
            byte r = (byte)Math.Max(0, (int)(c.R * f));
            byte g = (byte)Math.Max(0, (int)(c.G * f));
            byte b = (byte)Math.Max(0, (int)(c.B * f));
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
