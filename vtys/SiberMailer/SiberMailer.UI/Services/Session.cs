using SiberMailer.Core.Models;

namespace SiberMailer.UI.Services;

/// <summary>
/// Static session manager for the currently logged-in user.
/// </summary>
public static class Session
{
    private static User? _currentUser;
    
    /// <summary>
    /// Gets or sets the currently logged-in user.
    /// </summary>
    public static User? CurrentUser
    {
        get => _currentUser;
        set => _currentUser = value;
    }
    
    /// <summary>
    /// Checks if a user is currently logged in.
    /// </summary>
    public static bool IsLoggedIn => _currentUser != null;

    /// <summary>
    /// Clears the current session.
    /// </summary>
    public static void Logout()
    {
        _currentUser = null;
    }
}
