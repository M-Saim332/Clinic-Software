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

    // Fields
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _role = "Receptionist";
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool PasswordVisible => Mode == FormMode.Add;
    public List<string> RoleOptions { get; } = new() { "Doctor", "Receptionist", "Admin" };

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new user details.";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedUser == null) { StatusMessage = "Select a user first."; return; }
        FillFields(SelectedUser);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedUser == null) { StatusMessage = "Select a user first."; return; }
        if (SelectedUser.UserID == CurrentUser?.UserID) { StatusMessage = "Cannot delete your own account."; return; }
        
        try
        {
            var ok = await Task.Run(() => _repo.Delete(SelectedUser.UserID));
            if (ok)
            {
                StatusMessage = "User deleted.";
                SelectedUser = null;
                await InitializeAsync();
            }
            else
            {
                StatusMessage = "Cannot delete — user has existing appointments.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
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
                await Task.Run(() => _repo.Insert(u, Password));
                StatusMessage = "User created.";
            }
            else
            {
                var u = SelectedUser!;
                u.Username = Username; u.FullName = FullName; u.Role = Role; u.IsActive = IsActive;
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
                TotalUsersCount = Users.Count;
                ActiveUsersCount = Users.Count(u => u.IsActive);
                AdminCount = Users.Count(u => u.Role == "Admin");
                DoctorCount = Users.Count(u => u.Role == "Doctor");
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
    }

    private void FillFields(User u)
    {
        Username = u.Username; FullName = u.FullName; Role = u.Role; IsActive = u.IsActive;
        Password = string.Empty; ConfirmPassword = string.Empty;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(PasswordVisible));
        OnPropertyChanged(nameof(PasswordWatermark));
    }
}
