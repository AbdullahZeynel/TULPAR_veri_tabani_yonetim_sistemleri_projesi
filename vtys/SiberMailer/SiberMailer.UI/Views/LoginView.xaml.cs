using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : Window
{
    public LoginView()
    {
        InitializeComponent();

        // Subscribe to login success event
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.LoginSuccess += OnLoginSuccess;
        }

        // Focus on username field when window loads
        Loaded += (s, e) => UsernameTextBox.Focus();
    }

    /// <summary>
    /// Allow dragging the window by clicking anywhere.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Handle password change (PasswordBox doesn't support binding for security).
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SetPassword(passwordBox.Password);
        }
    }

    /// <summary>
    /// Handle successful login - navigate to main window.
    /// </summary>
    private void OnLoginSuccess(object? sender, LoginSuccessEventArgs e)
    {
        // Open main window with authenticated user
        var mainWindow = new MainWindow(e.User, e.AuthService);
        mainWindow.Show();

        // Close login window
        Close();
    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {

    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
