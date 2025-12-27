namespace SiberMailer.UI.ViewModels;

/// <summary>
/// Interface for views that support data refresh.
/// Implement this in UserControls/ViewModels that need auto-refresh on navigation.
/// </summary>
public interface IRefreshable
{
    /// <summary>
    /// Asynchronously refreshes data from the database.
    /// Called automatically when the view becomes visible.
    /// </summary>
    Task RefreshData();
}
