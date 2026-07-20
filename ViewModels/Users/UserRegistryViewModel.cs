using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;

namespace ClinicSystem.UI.ViewModels.Users;

/// <summary>Controls which sub-view is shown within User Management.</summary>
public enum UserMgmtView { List, Form, ResetPassword }

public partial class UserRegistryViewModel : ViewModelBase
{
    private readonly UserRepository _repo;

    public UserRegistryViewModel(UserRepository repo) => _repo = repo;

    // ── View State ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    [NotifyPropertyChangedFor(nameof(IsFormView))]
    [NotifyPropertyChangedFor(nameof(IsResetPasswordView))]
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    [NotifyPropertyChangedFor(nameof(PasswordVisible))]
    [NotifyPropertyChangedFor(nameof(PasswordWatermark))]
    private UserMgmtView _view = UserMgmtView.List;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    [NotifyPropertyChangedFor(nameof(PasswordVisible))]
    [NotifyPropertyChangedFor(nameof(PasswordWatermark))]
    private FormMode _mode = FormMode.View;

    public bool IsListView          => View == UserMgmtView.List;
    public bool IsFormView          => View == UserMgmtView.Form;
    public bool IsResetPasswordView => View == UserMgmtView.ResetPassword;

    public bool MutationEnabled   => View == UserMgmtView.List;
    public bool SaveCancelEnabled  => View == UserMgmtView.Form;
    public bool PasswordVisible    => Mode == FormMode.Add;
    public string PasswordWatermark => PasswordVisible ? "Required" : "Leave blank to keep current";

    // ── Status / Messages ─────────────────────────────────────────────────────

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSuccess;

    // ── Users Collection ──────────────────────────────────────────────────────

    private List<User> _allUsers = new();

    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private User? _selectedUser;

    // KPI counts
    [ObservableProperty] private int _totalUsersCount;
    [ObservableProperty] private int _activeUsersCount;
    [ObservableProperty] private int _adminCount;
    [ObservableProperty] private int _doctorCount;
    [ObservableProperty] private int _pharmacistCount;
    [ObservableProperty] private int _receptionistCount;

    // ── Search / Filter ───────────────────────────────────────────────────────

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterRole = "All";
    [ObservableProperty] private string _filterStatus = "All";

    public List<string> FilterRoleOptions   { get; } = new() { "All", "Admin", "Doctor", "Receptionist", "Pharmacist", "Assistant" };
    public List<string> FilterStatusOptions { get; } = new() { "All", "Active", "Inactive" };

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnFilterRoleChanged(string value)   => ApplyFilter();
    partial void OnFilterStatusChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allUsers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim().ToLowerInvariant();
            filtered = filtered.Where(u =>
                u.FullName.ToLowerInvariant().Contains(q) ||
                u.Username.ToLowerInvariant().Contains(q) ||
                (u.Email ?? "").ToLowerInvariant().Contains(q) ||
                (u.Phone ?? "").ToLowerInvariant().Contains(q) ||
                u.Role.ToLowerInvariant().Contains(q));
        }

        if (FilterRole != "All")
            filtered = filtered.Where(u => u.Role.Equals(FilterRole, StringComparison.OrdinalIgnoreCase));

        if (FilterStatus == "Active")
            filtered = filtered.Where(u => u.IsActive);
        else if (FilterStatus == "Inactive")
            filtered = filtered.Where(u => !u.IsActive);

        Users = new ObservableCollection<User>(filtered);
    }

    // ── Form Fields ───────────────────────────────────────────────────────────

    // Credentials
    [ObservableProperty] private string _username      = string.Empty;
    [ObservableProperty] private string _password      = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    // Personal
    [ObservableProperty] private string _fullName  = string.Empty;
    [ObservableProperty] private string _cnic      = string.Empty;
    [ObservableProperty] private string _phone     = string.Empty;
    [ObservableProperty] private string _email     = string.Empty;
    [ObservableProperty] private string _address   = string.Empty;
    [ObservableProperty] private string _gender    = string.Empty;
    [ObservableProperty] private DateTimeOffset? _dateOfBirth;

    // Professional
    [ObservableProperty] private string _designation    = string.Empty;
    [ObservableProperty] private string _qualification  = string.Empty;
    [ObservableProperty] private string _licenseNumber  = string.Empty;

    // Account
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowModuleAccess))]
    private string _role = "Receptionist";

    [ObservableProperty] private bool _isActive = true;

    // Profile Picture
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfilePicture))]
    private byte[]? _profilePictureData;
    public bool HasProfilePicture => ProfilePictureData?.Length > 0;

    // ── Module Access ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _accDashboard;
    [ObservableProperty] private bool _accPatients;
    [ObservableProperty] private bool _accAppointments;
    [ObservableProperty] private bool _accProducts;
    [ObservableProperty] private bool _accCompanies;
    [ObservableProperty] private bool _accSuppliers;
    [ObservableProperty] private bool _accPurchases;
    [ObservableProperty] private bool _accSales;
    [ObservableProperty] private bool _accReturns;
    [ObservableProperty] private bool _accInventory;
    [ObservableProperty] private bool _accReports;
    [ObservableProperty] private bool _accSearch;
    [ObservableProperty] private bool _accUsers;
    [ObservableProperty] private bool _accSettings;

    public bool ShowModuleAccess => Role != "Doctor" && Role != "Admin";

    public List<string> RoleOptions   { get; } = new() { "Admin", "Doctor", "Receptionist", "Pharmacist", "Assistant" };
    public List<string> GenderOptions { get; } = new() { "Male", "Female", "Other" };

    // Role auto-defaults
    partial void OnRoleChanged(string value)
    {
        if (Mode != FormMode.Add) return;

        // Reset everything
        AccDashboard = AccPatients = AccAppointments = AccProducts = AccCompanies =
        AccSuppliers = AccPurchases = AccSales = AccReturns =
        AccInventory = AccReports = AccSearch = AccUsers = AccSettings = false;

        switch (value)
        {
            case "Pharmacist":
                AccProducts = AccSales = AccInventory = AccPurchases = AccReturns = AccDashboard = true;
                break;
            case "Receptionist":
                AccDashboard = AccPatients = AccAppointments = AccSales = true;
                break;
            case "Doctor":
                // Full access — module checkboxes hidden
                break;
            case "Assistant":
                AccDashboard = AccPatients = AccAppointments = true;
                break;
        }
    }

    // ── Delete Confirmation ───────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    private User? _pendingDeleteUser;

    [ObservableProperty] private bool _showDeleteConfirm;

    public string PendingDeleteLabel => PendingDeleteUser is { } u
        ? (u.FullName.Length > 0 ? $"{u.FullName} (@{u.Username})" : $"@{u.Username}")
        : string.Empty;

    // ── Reset Password Fields ─────────────────────────────────────────────────

    [ObservableProperty] private User? _resetTarget;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmNewPassword = string.Empty;
    [ObservableProperty] private bool _forceChangeOnNextLogin;
    [ObservableProperty] private string _resetStatusMessage = string.Empty;

    public string ResetTargetLabel => ResetTarget != null
        ? $"{(ResetTarget.FullName.Length > 0 ? ResetTarget.FullName : ResetTarget.Username)} (@{ResetTarget.Username})"
        : string.Empty;

    // ═════════════════════════════════════════════════════════════════════════
    // COMMANDS — Navigation
    // ═════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void New()
    {
        ClearFormFields();
        Mode = FormMode.Add;
        View = UserMgmtView.Form;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Edit(User? user)
    {
        if (user == null) return;
        SelectedUser = user;
        Mode = FormMode.Edit;
        FillFormFields(user);
        View = UserMgmtView.Form;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Cancel()
    {
        View = UserMgmtView.List;
        Mode = FormMode.View;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ShowResetPassword(User? user)
    {
        if (user == null) return;
        ResetTarget = user;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        ForceChangeOnNextLogin = false;
        ResetStatusMessage = string.Empty;
        View = UserMgmtView.ResetPassword;
        OnPropertyChanged(nameof(ResetTargetLabel));
    }

    [RelayCommand]
    private void CancelResetPassword()
    {
        View = UserMgmtView.List;
        ResetStatusMessage = string.Empty;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COMMANDS — CRUD
    // ═════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username)) { StatusMessage = "Username is required."; IsSuccess = false; return; }
        if (string.IsNullOrWhiteSpace(FullName))  { StatusMessage = "Full Name is required."; IsSuccess = false; return; }
        if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains('@'))
        {
            StatusMessage = "Email address is not valid.";
            IsSuccess = false;
            return;
        }

        try
        {
            if (Mode == FormMode.Add)
            {
                if (string.IsNullOrWhiteSpace(Password)) { StatusMessage = "Password is required for a new user."; IsSuccess = false; return; }
                if (Password != ConfirmPassword) { StatusMessage = "Passwords do not match."; IsSuccess = false; return; }
                if (Password.Length < 8) { StatusMessage = "Password must be at least 8 characters."; IsSuccess = false; return; }

                var u = BuildUser();
                await Task.Run(() => _repo.Insert(u, Password));
                StatusMessage = $"User '{u.Username}' created successfully.";
                IsSuccess = true;
                LogActivity("User Created", $"New user '{u.Username}' ({u.Role}) created by admin", "Users");
            }
            else // Edit
            {
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    if (Password != ConfirmPassword) { StatusMessage = "Passwords do not match."; IsSuccess = false; return; }
                    if (Password.Length < 8) { StatusMessage = "Password must be at least 8 characters."; IsSuccess = false; return; }
                }

                var u = BuildUser();
                u.UserID = SelectedUser!.UserID;
                await Task.Run(() => _repo.AdminUpdateFull(u));

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    await Task.Run(() => _repo.ChangePassword(u.UserID, Password));
                }

                StatusMessage = $"User '{u.Username}' updated successfully.";
                IsSuccess = true;
                LogActivity("User Updated", $"User '{u.Username}' profile updated by admin", "Users");
            }

            View = UserMgmtView.List;
            Mode = FormMode.View;
            await InitializeAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            IsSuccess = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    private async Task ToggleStatusAsync(User? user)
    {
        if (user == null) return;
        if (user.IsAdmin) { StatusMessage = "Cannot deactivate the Administrator account."; IsSuccess = false; return; }

        var newStatus = !user.IsActive;
        try
        {
            await Task.Run(() => _repo.ToggleStatus(user.UserID, newStatus));
            var verb = newStatus ? "activated" : "deactivated";
            StatusMessage = $"User '{user.Username}' {verb}.";
            IsSuccess = true;
            LogActivity(newStatus ? "User Activated" : "User Deactivated",
                $"User '{user.Username}' was {verb} by admin", "Users");
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Status change failed: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    private async Task SaveResetPasswordAsync()
    {
        if (ResetTarget == null) return;
        if (string.IsNullOrWhiteSpace(NewPassword)) { ResetStatusMessage = "New password is required."; return; }
        if (NewPassword != ConfirmNewPassword) { ResetStatusMessage = "Passwords do not match."; return; }
        if (NewPassword.Length < 8) { ResetStatusMessage = "Password must be at least 8 characters."; return; }

        try
        {
            await Task.Run(() => _repo.AdminResetPassword(ResetTarget.UserID, NewPassword, ForceChangeOnNextLogin));
            LogActivity("Password Reset",
                $"Password for '{ResetTarget.Username}' was reset by admin", "Users");

            StatusMessage = $"Password for '{ResetTarget.Username}' reset successfully.";
            IsSuccess = true;
            View = UserMgmtView.List;
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            ResetStatusMessage = $"Reset failed: {ex.Message}";
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void RequestDelete(User? user)
    {
        if (user == null) return;
        PendingDeleteUser = user;
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var target = PendingDeleteUser;
        ShowDeleteConfirm = false;
        PendingDeleteUser = null;
        if (target == null) return;
        if (target.UserID == CurrentUser?.UserID) { StatusMessage = "Cannot delete your own account."; IsSuccess = false; return; }

        try
        {
            var ok = await Task.Run(() => _repo.Delete(target.UserID));
            if (ok)
            {
                StatusMessage = $"User '{target.Username}' deleted.";
                IsSuccess = true;
                LogActivity("User Deleted", $"User '{target.Username}' was deleted by admin", "Users");
                SelectedUser = null;
                await InitializeAsync();
            }
            else
            {
                StatusMessage = "Cannot delete — user has existing patient records.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
        PendingDeleteUser = null;
    }

    // ── Profile Picture ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task UploadPictureAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var storage = desktop.MainWindow?.StorageProvider;
                if (storage == null) return;

                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Profile Picture",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp" } }
                    }
                });

                if (files.Count > 0)
                {
                    using var stream = await files[0].OpenReadAsync();
                    using var ms = new System.IO.MemoryStream();
                    await stream.CopyToAsync(ms);
                    ProfilePictureData = ms.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    private void RemovePicture() => ProfilePictureData = null;

    // ═════════════════════════════════════════════════════════════════════════
    // DATA LOAD
    // ═════════════════════════════════════════════════════════════════════════

    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _allUsers = list.ToList();
                TotalUsersCount    = _allUsers.Count;
                ActiveUsersCount   = _allUsers.Count(u => u.IsActive);
                AdminCount         = _allUsers.Count(u => u.Role == "Admin");
                DoctorCount        = _allUsers.Count(u => u.Role == "Doctor");
                PharmacistCount    = _allUsers.Count(u => u.Role == "Pharmacist");
                ReceptionistCount  = _allUsers.Count(u => u.Role == "Receptionist");
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load users: {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private User BuildUser() => new()
    {
        Username       = Username.Trim(),
        FullName       = FullName.Trim(),
        Email          = string.IsNullOrWhiteSpace(Email)          ? null : Email.Trim(),
        Phone          = string.IsNullOrWhiteSpace(Phone)          ? null : Phone.Trim(),
        CNIC           = string.IsNullOrWhiteSpace(Cnic)           ? null : Cnic.Trim(),
        Address        = string.IsNullOrWhiteSpace(Address)        ? null : Address.Trim(),
        Gender         = string.IsNullOrWhiteSpace(Gender)         ? null : Gender,
        Designation    = string.IsNullOrWhiteSpace(Designation)    ? null : Designation.Trim(),
        Qualification  = string.IsNullOrWhiteSpace(Qualification)  ? null : Qualification.Trim(),
        LicenseNumber  = string.IsNullOrWhiteSpace(LicenseNumber)  ? null : LicenseNumber.Trim(),
        DateOfBirth    = DateOfBirth?.DateTime,
        ProfilePicture = ProfilePictureData,
        Role           = Role,
        IsActive       = IsActive,
        Permissions    = GetPermissionsString()
    };

    private void ClearFormFields()
    {
        Username = FullName = Email = Phone = Cnic = Address = string.Empty;
        Gender = Designation = Qualification = LicenseNumber = string.Empty;
        Password = ConfirmPassword = string.Empty;
        Role = "Receptionist";
        IsActive = true;
        DateOfBirth = null;
        ProfilePictureData = null;
        AccDashboard = AccPatients = AccAppointments = AccProducts = AccCompanies =
        AccSuppliers = AccPurchases = AccSales = AccReturns =
        AccInventory = AccReports = AccSearch = AccUsers = AccSettings = false;
        // Default permissions for Receptionist
        AccDashboard = AccPatients = AccAppointments = AccSales = true;
        StatusMessage = string.Empty;
    }

    private void FillFormFields(User u)
    {
        Username      = u.Username;
        FullName      = u.FullName;
        Email         = u.Email         ?? string.Empty;
        Phone         = u.Phone         ?? string.Empty;
        Cnic          = u.CNIC          ?? string.Empty;
        Address       = u.Address       ?? string.Empty;
        Gender        = u.Gender        ?? string.Empty;
        Designation   = u.Designation   ?? string.Empty;
        Qualification = u.Qualification ?? string.Empty;
        LicenseNumber = u.LicenseNumber ?? string.Empty;
        DateOfBirth   = u.DateOfBirth.HasValue ? new DateTimeOffset(u.DateOfBirth.Value) : null;
        ProfilePictureData = u.ProfilePicture;
        Role     = u.Role;
        IsActive = u.IsActive;
        Password = ConfirmPassword = string.Empty;

        var p = u.Permissions ?? "";
        AccDashboard    = p.Contains("Dashboard");
        AccPatients     = p.Contains("Patients");
        AccAppointments = p.Contains("Appointments");
        AccProducts     = p.Contains("Products");
        AccCompanies    = p.Contains("Companies");
        AccSuppliers    = p.Contains("Suppliers");
        AccPurchases    = p.Contains("Purchases");
        AccSales        = p.Contains("Sales");
        AccReturns      = p.Contains("Returns");
        AccInventory    = p.Contains("Inventory");
        AccReports      = p.Contains("Reports");
        AccSearch       = p.Contains("Search");
        AccUsers        = p.Contains("Users");
        AccSettings     = p.Contains("Settings");
    }

    private string GetPermissionsString()
    {
        var p = new List<string>();
        if (AccDashboard)    p.Add("Dashboard");
        if (AccPatients)     p.Add("Patients");
        if (AccAppointments) p.Add("Appointments");
        if (AccProducts)     p.Add("Products");
        if (AccCompanies)    p.Add("Companies");
        if (AccSuppliers)    p.Add("Suppliers");
        if (AccPurchases)    p.Add("Purchases");
        if (AccSales)        p.Add("Sales");
        if (AccReturns)      p.Add("Returns");
        if (AccInventory)    p.Add("Inventory");
        if (AccReports)      p.Add("Reports");
        if (AccSearch)       p.Add("Search");
        if (AccUsers)        p.Add("Users");
        if (AccSettings)     p.Add("Settings");
        return string.Join(",", p);
    }
}
