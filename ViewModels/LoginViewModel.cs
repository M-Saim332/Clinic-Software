using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Data.Repositories;
using ClinicSystem.Core.Models;

namespace ClinicSystem.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly UserRepository _userRepo;

    public LoginViewModel(UserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginButtonText))]
    [NotifyPropertyChangedFor(nameof(ToggleModeText))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(FormSubtitle))]
    private bool _isSignUpMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginButtonText))]
    private bool _isBusy;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);
    
    public string FormTitle => IsSignUpMode ? "Create Account" : "Clinic Management";
    public string FormSubtitle => IsSignUpMode ? "Sign up to continue" : "Sign in to continue";
    public string LoginButtonText => IsBusy ? (IsSignUpMode ? "Signing up…" : "Signing in…") : (IsSignUpMode ? "Sign Up" : "Sign In");
    public string ToggleModeText => IsSignUpMode ? "Already have an account? Sign in" : "Don't have an account? Sign up";

    public event Action<User>? LoginSucceeded;

    [RelayCommand]
    private void ToggleMode()
    {
        IsSignUpMode = !IsSignUpMode;
        ErrorMessage = string.Empty;
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
            if (IsSignUpMode)
            {
                if (Password != ConfirmPassword)
                {
                    ErrorMessage = "Passwords do not match.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    ErrorMessage = "Full Name is required.";
                    return;
                }

                var existing = await Task.Run(() => _userRepo.GetByUsername(Username));
                if (existing != null)
                {
                    ErrorMessage = "Username is already taken.";
                    return;
                }

                var newUser = new User { Username = Username, FullName = FullName, Role = "Receptionist", IsActive = true };
                var newId = await Task.Run(() => _userRepo.Insert(newUser, Password));
                newUser.UserID = newId;

                CurrentUser = newUser;
                LoginSucceeded?.Invoke(newUser);
            }
            else
            {
                var user = await Task.Run(() => _userRepo.Authenticate(Username, Password));
                if (user == null)
                {
                    ErrorMessage = "Invalid username or password.";
                }
                else
                {
                    CurrentUser = user;
                    LoginSucceeded?.Invoke(user);
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
