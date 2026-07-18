using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClinicSystem.UI.ViewModels.Profile;

public partial class ProfileViewModel : ViewModelBase
{
    private readonly UserRepository _userRepo;

    public ProfileViewModel(UserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    // ── Form State ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSuccess;

    // ── Profile Picture ────────────────────────────────────────────────────
    [ObservableProperty] private byte[]? _profilePicture;
    [ObservableProperty] private bool _hasProfilePicture;

    // ── Profile Fields ─────────────────────────────────────────────────────
    [ObservableProperty] private string _fullName       = string.Empty;
    [ObservableProperty] private string _username       = string.Empty;
    [ObservableProperty] private string _email          = string.Empty;
    [ObservableProperty] private string _phone          = string.Empty;
    [ObservableProperty] private string _cnic           = string.Empty;
    [ObservableProperty] private string _address        = string.Empty;
    [ObservableProperty] private string _gender         = string.Empty;
    [ObservableProperty] private string _qualification  = string.Empty;
    [ObservableProperty] private string _designation    = string.Empty;
    [ObservableProperty] private string _licenseNumber  = string.Empty;
    [ObservableProperty] private DateTimeOffset? _dateOfBirth;
    [ObservableProperty] private string _role           = string.Empty;

    // ── Change Password ────────────────────────────────────────────────────
    [ObservableProperty] private bool _showChangePassword;
    [ObservableProperty] private string _oldPassword    = string.Empty;
    [ObservableProperty] private string _newPassword    = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _passwordStatusMessage = string.Empty;
    [ObservableProperty] private bool _isPasswordSuccess;

    public bool IsReadOnly => !IsEditing;
    public string[] GenderOptions => new[] { "Male", "Female", "Other" };

    public void LoadFromCurrentUser()
    {
        if (CurrentUser == null) return;

        FullName      = CurrentUser.FullName ?? string.Empty;
        Username      = CurrentUser.Username ?? string.Empty;
        Email         = CurrentUser.Email ?? string.Empty;
        Phone         = CurrentUser.Phone ?? string.Empty;
        Cnic          = CurrentUser.CNIC ?? string.Empty;
        Address       = CurrentUser.Address ?? string.Empty;
        Gender        = CurrentUser.Gender ?? string.Empty;
        Qualification = CurrentUser.Qualification ?? string.Empty;
        Designation   = CurrentUser.Designation ?? string.Empty;
        LicenseNumber = CurrentUser.LicenseNumber ?? string.Empty;
        Role          = CurrentUser.Role ?? string.Empty;
        DateOfBirth   = CurrentUser.DateOfBirth.HasValue
                            ? new DateTimeOffset(CurrentUser.DateOfBirth.Value)
                            : null;

        if (CurrentUser.ProfilePicture != null && CurrentUser.ProfilePicture.Length > 0)
        {
            ProfilePicture = CurrentUser.ProfilePicture;
            HasProfilePicture = true;
        }
        else
        {
            ProfilePicture = null;
            HasProfilePicture = false;
        }

        IsEditing = false;
        ShowChangePassword = false;
        StatusMessage = string.Empty;
        PasswordStatusMessage = string.Empty;
        OnPropertyChanged(nameof(IsReadOnly));
    }

    [RelayCommand]
    private void StartEdit()
    {
        IsEditing = true;
        ShowChangePassword = false;
        OnPropertyChanged(nameof(IsReadOnly));
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        LoadFromCurrentUser();
        IsEditing = false;
        OnPropertyChanged(nameof(IsReadOnly));
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Full name is required.";
            IsSuccess = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "Username is required.";
            IsSuccess = false;
            return;
        }

        try
        {
            var userId = CurrentUser!.UserID;
            var updatedUser = new User
            {
                UserID       = userId,
                FullName     = FullName,
                Username     = Username,
                Email        = Email,
                Phone        = Phone,
                CNIC         = Cnic,
                Address      = Address,
                Gender       = Gender,
                Qualification = Qualification,
                Designation  = Designation,
                LicenseNumber = LicenseNumber,
                DateOfBirth  = DateOfBirth?.DateTime,
                ProfilePicture = ProfilePicture,
                Role         = CurrentUser.Role,
                PasswordHash = CurrentUser.PasswordHash,
                IsActive     = CurrentUser.IsActive,
                Permissions  = CurrentUser.Permissions
            };

            await Task.Run(() => _userRepo.UpdateFullProfile(userId, updatedUser));

            var refreshed = await Task.Run(() => _userRepo.GetById(userId));
            if (refreshed != null) CurrentUser = refreshed;

            IsEditing = false;
            IsSuccess = true;
            StatusMessage = "Profile saved successfully!";
            OnPropertyChanged(nameof(IsReadOnly));
            LogActivity("Profile Updated", $"User '{Username}' updated their profile", "Profile");
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            StatusMessage = $"Error saving profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UploadProfilePictureAsync()
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
                    new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
                }
            });

            if (files?.Count > 0)
            {
                var file = files[0];
                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ProfilePicture = ms.ToArray();
                HasProfilePicture = true;
                StatusMessage = "Picture loaded — click Save to apply.";
            }
        }
    }

    [RelayCommand]
    private void RemoveProfilePicture()
    {
        ProfilePicture = null;
        HasProfilePicture = false;
        StatusMessage = "Picture removed — click Save to apply.";
    }

    // ── Change Password ────────────────────────────────────────────────────
    [RelayCommand]
    private void OpenChangePassword()
    {
        IsEditing = false;
        OldPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        PasswordStatusMessage = string.Empty;
        ShowChangePassword = true;
        OnPropertyChanged(nameof(IsReadOnly));
    }

    [RelayCommand]
    private void CloseChangePassword()
    {
        ShowChangePassword = false;
        PasswordStatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SavePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            PasswordStatusMessage = "All password fields are required.";
            IsPasswordSuccess = false;
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            PasswordStatusMessage = "New password and confirmation do not match.";
            IsPasswordSuccess = false;
            return;
        }
        if (NewPassword.Length < 6)
        {
            PasswordStatusMessage = "New password must be at least 6 characters.";
            IsPasswordSuccess = false;
            return;
        }

        try
        {
            var verified = await Task.Run(() => _userRepo.Authenticate(CurrentUser!.Username, OldPassword));
            if (verified == null)
            {
                PasswordStatusMessage = "Current password is incorrect.";
                IsPasswordSuccess = false;
                return;
            }

            await Task.Run(() => _userRepo.ChangePassword(CurrentUser!.UserID, NewPassword));
            IsPasswordSuccess = true;
            PasswordStatusMessage = "Password changed successfully!";
            LogActivity("Password Changed", "User changed their password", "Profile");

            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (Exception ex)
        {
            IsPasswordSuccess = false;
            PasswordStatusMessage = $"Error: {ex.Message}";
        }
    }
}
