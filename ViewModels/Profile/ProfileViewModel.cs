using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.ViewModels.Profile;

public partial class ProfileViewModel : ViewModelBase
{
    private readonly UserRepository _repo;

    public ProfileViewModel(UserRepository repo)
    {
        _repo = repo;
    }

    // ── Profile fields ──────────────────────────────────────────────────────
    [ObservableProperty] private string _fullName    = string.Empty;
    [ObservableProperty] private string _username    = string.Empty;
    [ObservableProperty] private string _role        = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isSuccess;

    // ── Password Change fields ──────────────────────────────────────────────
    [ObservableProperty] private string _currentPassword  = string.Empty;
    [ObservableProperty] private string _newPassword      = string.Empty;
    [ObservableProperty] private string _confirmPassword  = string.Empty;
    [ObservableProperty] private string _passwordStatus   = string.Empty;
    [ObservableProperty] private bool   _isPasswordSuccess;

    // ── Edit mode ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isEditingProfile;
    [ObservableProperty] private bool _isChangingPassword;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;
    public string RoleDisplay => Role;

    public void LoadFromCurrentUser()
    {
        if (CurrentUser == null) return;
        FullName = CurrentUser.FullName ?? string.Empty;
        Username = CurrentUser.Username ?? string.Empty;
        Role     = CurrentUser.Role     ?? string.Empty;
        StatusMessage   = string.Empty;
        PasswordStatus  = string.Empty;
        IsEditingProfile   = false;
        IsChangingPassword = false;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RoleDisplay));
    }

    [RelayCommand]
    private void StartEditProfile()
    {
        IsEditingProfile   = true;
        IsChangingPassword = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEditProfile()
    {
        LoadFromCurrentUser();
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "Username cannot be empty.";
            IsSuccess = false;
            return;
        }

        try
        {
            await Task.Run(() => _repo.UpdateProfile(CurrentUser!.UserID, FullName.Trim(), Username.Trim()));

            // Update in-session object
            CurrentUser!.FullName = FullName.Trim();
            CurrentUser.Username  = Username.Trim();

            LogActivity("Profile Updated", $"User '{Username}' updated their profile", "Profile");

            IsEditingProfile = false;
            StatusMessage = "Profile updated successfully.";
            IsSuccess = true;
            OnPropertyChanged(nameof(DisplayName));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating profile: {ex.Message}";
            IsSuccess = false;
        }
    }

    [RelayCommand]
    private void StartChangePassword()
    {
        IsChangingPassword = true;
        IsEditingProfile   = false;
        CurrentPassword    = string.Empty;
        NewPassword        = string.Empty;
        ConfirmPassword    = string.Empty;
        PasswordStatus     = string.Empty;
    }

    [RelayCommand]
    private void CancelChangePassword()
    {
        IsChangingPassword = false;
        CurrentPassword    = string.Empty;
        NewPassword        = string.Empty;
        ConfirmPassword    = string.Empty;
        PasswordStatus     = string.Empty;
    }

    [RelayCommand]
    private async Task SavePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            PasswordStatus = "Current password is required.";
            IsPasswordSuccess = false;
            return;
        }
        if (NewPassword.Length < 6)
        {
            PasswordStatus = "New password must be at least 6 characters.";
            IsPasswordSuccess = false;
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            PasswordStatus = "Passwords do not match.";
            IsPasswordSuccess = false;
            return;
        }

        try
        {
            // Verify current password
            var verified = await Task.Run(() => _repo.Authenticate(CurrentUser!.Username!, CurrentPassword));
            if (verified == null)
            {
                PasswordStatus = "Current password is incorrect.";
                IsPasswordSuccess = false;
                return;
            }

            await Task.Run(() => _repo.ChangePassword(CurrentUser!.UserID, NewPassword));
            LogActivity("Password Changed", $"User '{Username}' changed their password", "Profile");

            IsChangingPassword = false;
            PasswordStatus     = "Password changed successfully.";
            IsPasswordSuccess  = true;
            CurrentPassword    = string.Empty;
            NewPassword        = string.Empty;
            ConfirmPassword    = string.Empty;
        }
        catch (Exception ex)
        {
            PasswordStatus = $"Error: {ex.Message}";
            IsPasswordSuccess = false;
        }
    }
}
