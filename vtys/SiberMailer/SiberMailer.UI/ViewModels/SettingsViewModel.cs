using SiberMailer.Core.Models;
using SiberMailer.Data;
using SiberMailer.Data.Repositories;
using SiberMailer.UI.Services;
using SiberMailer.UI.Views;
using System.Windows;
using System.Windows.Input;

namespace SiberMailer.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly UserRepository _userRepository;
    private string _oldPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isProcessing;

    public SettingsViewModel()
    {
        var factory = new DbConnectionFactory(DbConnectionFactory.DefaultConnectionString);
        _userRepository = new UserRepository(factory);

        ChangePasswordCommand = new AsyncRelayCommand(ExecuteChangePasswordAsync, CanChangePassword);
        LogoutCommand = new RelayCommand(_ => ExecuteLogout());
    }

    #region Properties

    public string CurrentUserName => Session.CurrentUser?.FullName ?? "Unknown User";

    public string CurrentBranch => "Sponsorluk Birimi"; // Hardcoded as per requirements

    public string AppVersion => "v2.0.1";

    public string OldPassword
    {
        get => _oldPassword;
        set => SetProperty(ref _oldPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    #endregion

    #region Commands

    public ICommand ChangePasswordCommand { get; }
    public ICommand LogoutCommand { get; }

    private bool CanChangePassword()
    {
        return !IsProcessing && 
               !string.IsNullOrWhiteSpace(OldPassword) && 
               !string.IsNullOrWhiteSpace(NewPassword) && 
               !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    private async Task ExecuteChangePasswordAsync()
    {
        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "❌ New passwords do not match.";
            return;
        }

        if (Session.CurrentUser == null)
        {
            StatusMessage = "❌ No user logged in.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Updating password...";

        try
        {
            // 1. Verify old password
            var user = await _userRepository.LoginAsync(Session.CurrentUser.Username, OldPassword);
            if (user == null)
            {
                StatusMessage = "❌ Incorrect old password.";
                IsProcessing = false;
                return;
            }

            // 2. Hash new password
            var newHash = UserRepository.HashPassword(NewPassword);

            // 3. Update in database
            var success = await _userRepository.UpdatePasswordAsync(Session.CurrentUser.UserId, newHash);

            if (success)
            {
                StatusMessage = "✅ Password updated successfully!";
                OldPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                // Note: Clear logic would be handled by View update usually
            }
            else
            {
                StatusMessage = "❌ Update failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void ExecuteLogout()
    {
        // Clear session
        Session.Logout();

        // Navigate to Login View
        // Since we are in a Frame or Window, we need to find the parent window and replace usage
        // Or restart application. 
        // Best approach: Open LoginWindow and close current MainWindow.
        
        var loginView = new LoginView();
        loginView.Show();
        
        Application.Current.MainWindow?.Close();
        Application.Current.MainWindow = loginView;
    }

    #endregion
}
