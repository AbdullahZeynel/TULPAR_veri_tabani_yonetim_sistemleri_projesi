using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for SmtpConfigView.xaml
/// </summary>
public partial class SmtpConfigView : UserControl, IRefreshable
{
    public SmtpConfigView()
    {
        InitializeComponent();
        
        // Auto-refresh on visibility change
        IsVisibleChanged += OnVisibilityChanged;
    }

    /// <summary>
    /// Refreshes the SMTP accounts data from the database.
    /// </summary>
    public async Task RefreshData()
    {
        if (DataContext is SmtpConfigViewModel vm)
        {
            await vm.LoadAccountsAsync();
        }
    }

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            await RefreshData();
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SmtpConfigViewModel vm && sender is PasswordBox pb)
        {
            vm.Password = pb.Password;
        }
    }
}
