using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Users;

public partial class UserRegistryViewModel : ViewModelBase
{
    private readonly UserRepository _repo;

    public UserRegistryViewModel(UserRepository repo)
    {
        _repo = repo;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MutationEnabled))]
    [NotifyPropertyChangedFor(nameof(SaveCancelEnabled))]
    [NotifyPropertyChangedFor(nameof(PasswordVisible))]
    [NotifyPropertyChangedFor(nameof(PasswordWatermark))]
    private FormMode _mode = FormMode.View;

    public string PasswordWatermark => PasswordVisible ? "Required for new user" : "Leave blank to keep current";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private User? _selectedUser;

    // KPI summary counts
    [ObservableProperty] private int _totalUsersCount;
    [ObservableProperty] private int _activeUsersCount;
    [ObservableProperty] private int _adminCount;
    [ObservableProperty] private int _doctorCount;
    [ObservableProperty] private int _pharmacistCount;

    // Fields
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowModuleAccess))]
    private string _role = "Receptionist";

    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    // Module Access properties
    [ObservableProperty] private bool _accDashboard;
    [ObservableProperty] private bool _accPatients;
    [ObservableProperty] private bool _accAppointments;
    [ObservableProperty] private bool _accMedicines;
    [ObservableProperty] private bool _accProducts;
    [ObservableProperty] private bool _accCompanies;
    [ObservableProperty] private bool _accSuppliers;
    [ObservableProperty] private bool _accPurchases;
    [ObservableProperty] private bool _accSales;
    [ObservableProperty] private bool _accReturns;
    [ObservableProperty] private bool _accNewVisit;
    [ObservableProperty] private bool _accVisitHistory;
    [ObservableProperty] private bool _accInventory;
    [ObservableProperty] private bool _accReports;
    [ObservableProperty] private bool _accSearch;
    [ObservableProperty] private bool _accUsers;
    [ObservableProperty] private bool _accSettings;

    // Delete confirmation
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    private User? _pendingDeleteUser;
    [ObservableProperty] private bool _showDeleteConfirm;
    public string PendingDeleteLabel => PendingDeleteUser is { } u
        ? (u.FullName.Length > 0 ? $"{u.FullName} (@{u.Username})" : $"@{u.Username}")
        : string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool PasswordVisible => Mode == FormMode.Add;
    public bool ShowModuleAccess => Role != "Doctor" && Role != "Admin";
    public List<string> RoleOptions { get; } = new() { "Doctor", "Receptionist", "Pharmacist", "Admin" };

    // Auto-set sensible default permissions when switching to Pharmacist in Add mode
    partial void OnRoleChanged(string value)
    {
        if (value == "Pharmacist" && Mode == FormMode.Add)
        {
            AccDashboard = AccPatients = AccAppointments = AccCompanies =
            AccSuppliers = AccPurchases = AccReturns = AccNewVisit =
            AccVisitHistory = AccInventory = AccReports = AccSearch =
            AccUsers = AccSettings = false;
            AccMedicines = true;
            AccProducts  = true;
            AccSales     = true;
        }
    }

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new user details.";
    }

    [RelayCommand]
    private void Edit(User? user)
    {
        if (user == null) return;
        SelectedUser = user;
        Mode = FormMode.Edit;
        FillFields(user);
        NotifyButtonStates();
    }

    // Step 1: show confirmation dialog
    [RelayCommand]
    private void RequestDelete(User? user)
    {
        if (user == null) return;
        PendingDeleteUser = user;
        ShowDeleteConfirm = true;
    }

    // Step 2a: confirmed
    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var targetUser = PendingDeleteUser;
        ShowDeleteConfirm = false;
        PendingDeleteUser = null;
        if (targetUser == null) return;
        if (targetUser.UserID == CurrentUser?.UserID) { StatusMessage = "Cannot delete your own account."; return; }

        try
        {
            var ok = await Task.Run(() => _repo.Delete(targetUser.UserID));
            if (ok)
            {
                StatusMessage = "User deleted successfully.";
                SelectedUser = null;
                await InitializeAsync();
            }
            else
            {
                StatusMessage = "Cannot delete — user has existing records.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    // Step 2b: cancelled
    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
        PendingDeleteUser = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username)) { StatusMessage = "Username required."; return; }
        
        try
        {
            if (Mode == FormMode.Add)
            {
                if (Password != ConfirmPassword) { StatusMessage = "Passwords do not match."; return; }
                if (Password.Length < 6) { StatusMessage = "Password must be at least 6 characters."; return; }
                var u = new User { Username = Username, FullName = FullName, Role = Role, IsActive = IsActive };
                u.Permissions = GetPermissionsString();
                await Task.Run(() => _repo.Insert(u, Password));
                StatusMessage = "User created.";
            }
            else
            {
                var u = SelectedUser!;
                u.Username = Username; u.FullName = FullName; u.Role = Role; u.IsActive = IsActive;
                u.Permissions = GetPermissionsString();
                await Task.Run(() => _repo.Update(u));
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    if (Password != ConfirmPassword) { StatusMessage = "Passwords do not match."; return; }
                    await Task.Run(() => _repo.ChangePassword(u.UserID, Password));
                }
                StatusMessage = "User updated.";
            }
            Mode = FormMode.View;
            NotifyButtonStates();
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving user: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = string.Empty;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var usersList = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                Users = new ObservableCollection<User>(usersList);
                TotalUsersCount   = Users.Count;
                ActiveUsersCount  = Users.Count(u => u.IsActive);
                AdminCount        = Users.Count(u => u.Role == "Admin");
                DoctorCount       = Users.Count(u => u.Role == "Doctor");
                PharmacistCount   = Users.Count(u => u.Role == "Pharmacist");
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Failed to load users: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        Username = string.Empty; FullName = string.Empty; Role = "Receptionist"; IsActive = true;
        Password = string.Empty; ConfirmPassword = string.Empty;
        AccDashboard = AccPatients = AccAppointments = AccMedicines = AccProducts = AccCompanies = AccSuppliers = 
        AccPurchases = AccSales = AccReturns = AccNewVisit = AccVisitHistory = AccInventory = AccReports = AccSearch = AccUsers = AccSettings = false;
    }

    private void FillFields(User u)
    {
        Username = u.Username; FullName = u.FullName; Role = u.Role; IsActive = u.IsActive;
        Password = string.Empty; ConfirmPassword = string.Empty;
        var p = u.Permissions ?? "";
        AccDashboard = p.Contains("Dashboard");
        AccPatients = p.Contains("Patients");
        AccAppointments = p.Contains("Appointments");
        AccMedicines = p.Contains("Medicines");
        AccProducts = p.Contains("Products");
        AccCompanies = p.Contains("Companies");
        AccSuppliers = p.Contains("Suppliers");
        AccPurchases = p.Contains("Purchases");
        AccSales = p.Contains("Sales");
        AccReturns = p.Contains("Returns");
        AccNewVisit = p.Contains("NewVisit");
        AccVisitHistory = p.Contains("VisitHistory");
        AccInventory = p.Contains("Inventory");
        AccReports = p.Contains("Reports");
        AccSearch = p.Contains("Search");
        AccUsers = p.Contains("Users");
        AccSettings = p.Contains("Settings");
    }

    private string GetPermissionsString()
    {
        var p = new List<string>();
        if (AccDashboard) p.Add("Dashboard");
        if (AccPatients) p.Add("Patients");
        if (AccAppointments) p.Add("Appointments");
        if (AccMedicines) p.Add("Medicines");
        if (AccProducts) p.Add("Products");
        if (AccCompanies) p.Add("Companies");
        if (AccSuppliers) p.Add("Suppliers");
        if (AccPurchases) p.Add("Purchases");
        if (AccSales) p.Add("Sales");
        if (AccReturns) p.Add("Returns");
        if (AccNewVisit) p.Add("NewVisit");
        if (AccVisitHistory) p.Add("VisitHistory");
        if (AccInventory) p.Add("Inventory");
        if (AccReports) p.Add("Reports");
        if (AccSearch) p.Add("Search");
        if (AccUsers) p.Add("Users");
        if (AccSettings) p.Add("Settings");
        return string.Join(",", p);
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(PasswordVisible));
        OnPropertyChanged(nameof(PasswordWatermark));
    }
}
