namespace SiberMailer.UI.Services;

/// <summary>
/// Simple event aggregator for cross-ViewModel communication.
/// </summary>
public static class EventAggregator
{
    /// <summary>
    /// Raised when a new recipient list is created.
    /// Other ViewModels can subscribe to refresh their list of lists.
    /// </summary>
    public static event Action? ListCreated;

    /// <summary>
    /// Raised when a recipient list is deleted.
    /// </summary>
    public static event Action? ListDeleted;

    /// <summary>
    /// Raised when a recipient list is updated.
    /// </summary>
    public static event Action? ListUpdated;

    /// <summary>
    /// Raised when contacts are imported or changed.
    /// </summary>
    public static event Action<int>? ContactsChanged;

    public static void RaiseListCreated() => ListCreated?.Invoke();
    public static void RaiseListDeleted() => ListDeleted?.Invoke();
    public static void RaiseListUpdated() => ListUpdated?.Invoke();
    public static void RaiseContactsChanged(int listId) => ContactsChanged?.Invoke(listId);
}
