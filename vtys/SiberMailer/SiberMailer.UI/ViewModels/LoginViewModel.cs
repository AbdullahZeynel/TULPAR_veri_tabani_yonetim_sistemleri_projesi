using SiberMailer.Business.Services;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using SiberMailer.UI.Views;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

/// <summary>
/// ViewModel for the Login view.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _rememberMe;

    public LoginViewModel()
    {
        // Initialize services
        var connectionFactory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        var userRepository = new UserRepository(connectionFactory);
        _authService = new AuthService(userRepository);

        // Initialize commands
        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, CanExecuteLogin);
        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
        MinimizeCommand = new RelayCommand(_ => Application.Current.MainWindow?.SetCurrentValue(Window.WindowStateProperty, WindowState.Minimized));
    }

    #region Properties

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    #endregion

    #region Commands

    public ICommand LoginCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand MinimizeCommand { get; }

    /// <summary>
    /// Event raised when login is successful.
    /// </summary>
    public event EventHandler<LoginSuccessEventArgs>? LoginSuccess;

    private bool CanExecuteLogin()
    {
        return !IsLoading && 
               !string.IsNullOrWhiteSpace(Username) && 
               !string.IsNullOrWhiteSpace(Password);
    }

    private async Task ExecuteLoginAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _authService.LoginAsync(Username, Password);

            if (result.IsSuccess && result.User != null)
            {
                // Set static session for global access
                Session.CurrentUser = result.User;

                // Raise success event for view to handle navigation
                LoginSuccess?.Invoke(this, new LoginSuccessEventArgs(result.User, _authService));
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid credentials.";
                
                // Show themed error dialog
                CustomMessageBox.ShowError(
                    "Invalid credentials.",
                    "Login Failed");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
            
            // Show themed error dialog for connection errors
            CustomMessageBox.ShowError(
                $"Connection failed: {ex.Message}",
                "Connection Error");
        }
        finally
        {
            IsLoading = false;
            // Clear password for security
            Password = string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// Sets the password from the PasswordBox (for security, PasswordBox doesn't support binding).
    /// </summary>
    public void SetPassword(string password)
    {
        Password = password;
    }
}

/// <summary>
/// Event args for successful login.
/// </summary>
public class LoginSuccessEventArgs : EventArgs
{
    public SiberMailer.Core.Models.User User { get; }
    public AuthService AuthService { get; }

    public LoginSuccessEventArgs(SiberMailer.Core.Models.User user, AuthService authService)
    {
        User = user;
        AuthService = authService;
    }
}
