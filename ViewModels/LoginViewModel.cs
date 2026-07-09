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
                if (user != null)
                {
                    ProceedWithLogin(user);
                }
            }
            else
            {
                // Cancelled or failed, revert login
                CurrentUser = null;
            }
        }
    }

    private void ProceedWithLogin(User user)
    {
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

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);
    
    public string FormTitle => "Clinic Management";
    public string FormSubtitle => "Sign in to continue";
    public string LoginButtonText => IsBusy ? "Signing in..." : "Sign In";

    public event Action<User>? LoginSucceeded;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await Task.Run(() => _userRepo.Authenticate(Username, Password));
            if (user == null)
            {
                ErrorMessage = "Invalid username or password.";
            }
            else
            {
                if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && Password == "Admin@123")
                {
                    CurrentUser = user;
                    ChangePasswordVM.Reset();
                    ChangePasswordVM.CurrentPassword = Password;
                    IsForcePasswordChange = true;
                }
                else
                {
                    ProceedWithLogin(user);
                }
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
