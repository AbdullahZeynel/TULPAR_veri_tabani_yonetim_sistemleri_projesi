using SiberMailer.Business.Services;
using SiberMailer.Core.Models;
using SiberMailer.UI.ViewModels;
using SiberMailer.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SiberMailer.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private UserControl? _currentView;

    public MainWindow() : this(null, null) { }

    public MainWindow(User? user, AuthService? authService)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(user, authService);
        DataContext = _viewModel;

        // Subscribe to events
        _viewModel.LogoutRequested += OnLogoutRequested;
        _viewModel.MinimizeRequested += () => WindowState = WindowState.Minimized;
        _viewModel.MaximizeRequested += ToggleMaximize;
        _viewModel.CloseRequested += Close;
    }

    /// <summary>
    /// Handle title bar drag to move window.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    /// <summary>
    /// Toggle between maximized and normal window state.
    /// </summary>
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    /// <summary>
    /// Handle logout - return to login screen.
    /// </summary>
    private void OnLogoutRequested()
    {
        var loginView = new LoginView();
        loginView.Show();
        Close();
    }

    /// <summary>
    /// Navigates to the specified UserControl view.
    /// Clears the current content, sets the new view, and if 
    /// the view implements IRefreshable, awaits RefreshData().
    /// </summary>
    /// <param name="view">The UserControl to navigate to.</param>
    private async void NavigateTo(UserControl view)
    {
        // Store reference to current view
        _currentView = view;

        // If the view implements IRefreshable, refresh its data
        if (view is IRefreshable refreshableView)
        {
            await refreshableView.RefreshData();
        }
    }

    /// <summary>
    /// Navigates to SMTP Vault view.
    /// </summary>
    public void NavigateToSmtpVault()
    {
        NavigateTo(new SmtpVaultView());
    }

    /// <summary>
    /// Navigates to Templates view.
    /// </summary>
    public void NavigateToTemplates()
    {
        NavigateTo(new TemplatesView());
    }

    /// <summary>
    /// Navigates to Settings view.
    /// </summary>
    public void NavigateToSettings()
    {
        NavigateTo(new SettingsView());
    }
}
