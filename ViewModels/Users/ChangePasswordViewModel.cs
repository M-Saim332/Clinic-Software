using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.ViewModels.Users;

public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly UserRepository _userRepo;

    [ObservableProperty] private string _currentPassword  = string.Empty;
    [ObservableProperty] private string _newPassword      = string.Empty;
    [ObservableProperty] private string _confirmPassword  = string.Empty;
    [ObservableProperty] private string _statusMessage    = string.Empty;
    [ObservableProperty] private bool   _isSuccess;
    [ObservableProperty] private bool   _isBusy;

    public event Action? CloseRequested;

    public ChangePasswordViewModel(UserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public void Reset()
    {
        CurrentPassword  = string.Empty;
        NewPassword      = string.Empty;
        ConfirmPassword  = string.Empty;
        StatusMessage    = string.Empty;
        IsSuccess        = false;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        // Validation
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            StatusMessage = "Please enter your current password.";
            IsSuccess = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            StatusMessage = "New password must be at least 6 characters.";
            IsSuccess = false;
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "New passwords do not match.";
            IsSuccess = false;
            return;
        }
        if (NewPassword == CurrentPassword)
        {
            StatusMessage = "New password must be different from the current password.";
            IsSuccess = false;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var userId = CurrentUser?.UserID ?? 0;
            var success = await Task.Run(() =>
            {
                // Verify current password
                var user = _userRepo.Authenticate(CurrentUser?.Username ?? string.Empty, CurrentPassword);
                if (user == null) return false;

                _userRepo.ChangePassword(userId, NewPassword);
                return true;
            });

            if (success)
            {
                StatusMessage = "Password changed successfully!";
                IsSuccess = true;
                CurrentPassword  = string.Empty;
                NewPassword      = string.Empty;
                ConfirmPassword  = string.Empty;
            }
            else
            {
                StatusMessage = "Current password is incorrect.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
