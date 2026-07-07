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
    [ObservableProperty] private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginButtonText))]
    private bool _isBusy;

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
                CurrentUser = user;
                LoginSucceeded?.Invoke(user);
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
