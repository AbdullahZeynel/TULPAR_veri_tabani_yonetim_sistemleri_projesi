using SiberMailer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SiberMailer.UI.Views;

/// <summary>
/// Interaction logic for DashboardView.xaml
/// </summary>
public partial class DashboardView : UserControl, IRefreshable
{
    public DashboardView()
    {
        InitializeComponent();
        
        // Auto-refresh when the dashboard becomes visible
        IsVisibleChanged += OnVisibilityChanged;
        
        // Also refresh on Loaded event
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Refreshes dashboard data. Implementation of IRefreshable.
    /// </summary>
    public async Task RefreshData()
    {
        if (DataContext is DashboardViewModel viewModel)
        {
            await viewModel.RefreshAsync();
        }
    }

    /// <summary>
    /// Refreshes dashboard data when the control becomes visible.
    /// Provides "Always Fresh" experience when navigating back to dashboard.
    /// </summary>
    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            await RefreshData();
        }
    }

    /// <summary>
    /// Refreshes dashboard data when the control is loaded.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshData();
    }
}

