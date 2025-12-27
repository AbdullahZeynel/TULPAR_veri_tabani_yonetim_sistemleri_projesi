using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for SettingsView.xaml
/// </summary>
public partial class SettingsView : UserControl, IRefreshable
{
    public SettingsView()
    {
        InitializeComponent();
        
        // Auto-refresh on visibility change
        IsVisibleChanged += OnVisibilityChanged;

        // Wire up password boxes
        OldPasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        NewPasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        ConfirmPasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            if (sender == OldPasswordBox) vm.OldPassword = OldPasswordBox.Password;
            else if (sender == NewPasswordBox) vm.NewPassword = NewPasswordBox.Password;
            else if (sender == ConfirmPasswordBox) vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }

    /// <summary>
    /// Refreshes the settings data from the database.
    /// </summary>
    public Task RefreshData()
    {
        if (DataContext is SettingsViewModel vm)
        {
            // Clear passwords on refresh/view entry
            OldPasswordBox.Password = string.Empty;
            NewPasswordBox.Password = string.Empty;
            ConfirmPasswordBox.Password = string.Empty;
        }
        return Task.CompletedTask;
    }

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            await RefreshData();
        }
    }
}
