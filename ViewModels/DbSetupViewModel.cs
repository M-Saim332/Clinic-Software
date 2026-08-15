using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.UI.Services;

namespace ClinicSystem.UI.ViewModels;

public partial class DbSetupViewModel : ViewModelBase
{
    // ── Inputs ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreview))]
    private string _serverName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreview))]
    private string _databaseName = "ClinicDB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSqlAuth))]
    [NotifyPropertyChangedFor(nameof(ConnectionPreview))]
    private int _selectedAuthIndex = 0;  // 0 = Windows, 1 = SQL Server

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreview))]
    private string _sqlUsername = "sa";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreview))]
    private string _sqlPassword = string.Empty;

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    [ObservableProperty] private bool _isTestSuccess;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _initialError = string.Empty;
    [ObservableProperty] private bool _hasInitialError;

    // ── Computed ──────────────────────────────────────────────────────────────
    public bool IsSqlAuth => SelectedAuthIndex == 1;
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public string ConnectionPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ServerName)) return "(enter server name to preview)";
            try
            {
                var mode = IsSqlAuth ? AuthMode.SqlServer : AuthMode.Windows;
                return ConnectionStringHelper.Build(
                    ServerName.Trim(), DatabaseName.Trim(), mode, SqlUsername, SqlPassword);
            }
            catch { return string.Empty; }
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised when configuration is saved and the app should proceed to login.</summary>
    public event Action<string>? SetupCompleted;

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            StatusMessage  = "Please enter a server name or IP address.";
            IsTestSuccess  = false;
            return;
        }

        IsBusy        = true;
        StatusMessage = "Testing connection…";
        IsTestSuccess = false;

        var cs   = BuildCurrentConnectionString();
        var result = await Task.Run(() => ConnectionStringHelper.TestConnection(cs));

        IsTestSuccess = result.ok;
        StatusMessage = result.ok
            ? "✓ Connection successful! You can now save and continue."
            : $"✗ Connection failed: {result.error}";

        IsBusy = false;
    }

    [RelayCommand]
    private async Task SaveAndContinueAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            StatusMessage = "Please enter a server name or IP address.";
            return;
        }

        IsBusy        = true;
        StatusMessage = "Verifying connection…";

        var cs     = BuildCurrentConnectionString();
        var result = await Task.Run(() => ConnectionStringHelper.TestConnection(cs));

        if (!result.ok)
        {
            StatusMessage = $"✗ Cannot connect: {result.error}";
            IsTestSuccess  = false;
            IsBusy         = false;
            return;
        }

        // Persist and signal app to proceed
        try
        {
            ConnectionStringHelper.SaveToLocalJson(cs);
            StatusMessage = "✓ Settings saved. Loading application…";
            IsTestSuccess  = true;
            await Task.Delay(600); // brief visual confirmation
            SetupCompleted?.Invoke(cs);
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

    // ── Helpers ───────────────────────────────────────────────────────────────
    private string BuildCurrentConnectionString()
    {
        var mode = IsSqlAuth ? AuthMode.SqlServer : AuthMode.Windows;
        return ConnectionStringHelper.Build(
            ServerName.Trim(),
            string.IsNullOrWhiteSpace(DatabaseName) ? "ClinicDB" : DatabaseName.Trim(),
            mode,
            SqlUsername,
            SqlPassword);
    }

    /// <summary>Pre-populate fields if a partial connection string already exists.</summary>
    public void PrePopulateFromExisting(string? existingCs)
    {
        if (string.IsNullOrWhiteSpace(existingCs)) return;

        // Try to extract server from existing string
        var parts = existingCs.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim().ToLowerInvariant();
            var val = kv[1].Trim();
            switch (key)
            {
                case "server":
                case "data source":
                    ServerName = val;
                    break;
                case "database":
                case "initial catalog":
                    DatabaseName = val;
                    break;
                case "user id":
                case "uid":
                    SqlUsername        = val;
                    SelectedAuthIndex  = 1;
                    break;
            }
        }
    }
}
