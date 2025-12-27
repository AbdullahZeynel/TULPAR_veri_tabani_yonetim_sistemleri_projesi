using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for SmtpVaultView.xaml
/// </summary>
public partial class SmtpVaultView : UserControl, IRefreshable
{
    public SmtpVaultView()
    {
        InitializeComponent();
        
        // Wire up PasswordBox events (can't bind PasswordBox directly)
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Find and wire up password boxes
        var passwordBox = FindName("PasswordBox") as PasswordBox;
        var pinBox = FindName("PinBox") as PasswordBox;

        if (passwordBox != null)
        {
            passwordBox.PasswordChanged += (s, args) =>
            {
                if (DataContext is SmtpVaultViewModel vm)
                {
                    vm.DialogPassword = passwordBox.Password;
                }
            };
        }

        if (pinBox != null)
        {
            pinBox.PasswordChanged += (s, args) =>
            {
                if (DataContext is SmtpVaultViewModel vm)
                {
                    vm.DialogPin = pinBox.Password;
                }
            };
        }
    }

    /// <summary>
    /// Refreshes the SMTP accounts data from the database.
    /// </summary>
    public async Task RefreshData()
    {
        if (DataContext is SmtpVaultViewModel vm)
        {
            await vm.LoadAccountsAsync();
        }
    }
}
