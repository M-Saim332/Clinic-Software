using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Data.Repositories;
using ClinicSystem.Core.Models;
using ClinicSystem.UI.ViewModels.Users;

namespace ClinicSystem.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly UserRepository _userRepo;

    public ChangePasswordViewModel ChangePasswordVM { get; }

    public LoginViewModel(UserRepository userRepo, ChangePasswordViewModel changePasswordVM)
    {
        _userRepo = userRepo;
        ChangePasswordVM = changePasswordVM;
        ChangePasswordVM.CloseRequested += OnChangePasswordClosed;
    }

    private void OnChangePasswordClosed()
    {
        if (IsForcePasswordChange)
        {
            IsForcePasswordChange = false;
            if (ChangePasswordVM.IsSuccess)
            {
                var user = CurrentUser;
                if (user != null) ProceedWithLogin(user);
            }
            else
            {
                // Cancelled or failed — revert login
                CurrentUser = null;
            }
        }
    }

    private void ProceedWithLogin(User user)
    {
        // Record last login time in background
        Task.Run(() =>
        {
            try { _userRepo.RecordLastLogin(user.UserID); }
            catch { /* Non-critical — don't block login */ }
        });

        CurrentUser = user;
        ChangePasswordVM.CloseRequested -= OnChangePasswordClosed;
        LoginSucceeded?.Invoke(user);
    }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginButtonText))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isForcePasswordChange;

    /// <summary>True when the error is an account-deactivation notice (drives different styling).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private bool _isAccountDeactivated;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public string FormTitle    => "Clinic Management";
    public string FormSubtitle => "Sign in to continue";
    public string LoginButtonText => IsBusy ? "Signing in..." : "Sign In";

    public event Action<User>? LoginSucceeded;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage         = string.Empty;
        IsAccountDeactivated = false;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _userRepo.AuthenticateDetailed(Username, Password));

            switch (result.Reason)
            {
                case LoginBlockReason.None when result.User != null:
                    // Successful login
                    var user = result.User;
                    if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && Password == "Admin@123")
                    {
                        // Default password — force change
                        CurrentUser = user;
                        ChangePasswordVM.Reset();
                        ChangePasswordVM.CurrentPassword = Password;
                        IsForcePasswordChange = true;
                    }
                    else if (user.ForcePasswordChange)
                    {
                        // Admin reset password — force change on next login
                        CurrentUser = user;
                        ChangePasswordVM.Reset();
                        ChangePasswordVM.CurrentPassword = Password;
                        IsForcePasswordChange = true;
                    }
                    else
                    {
                        ProceedWithLogin(user);
                    }
                    break;

                case LoginBlockReason.AccountInactive:
                    IsAccountDeactivated = true;
                    ErrorMessage = "Your account has been deactivated. Please contact the administrator.";
                    break;

                default:
                    ErrorMessage = "Invalid username or password.";
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
