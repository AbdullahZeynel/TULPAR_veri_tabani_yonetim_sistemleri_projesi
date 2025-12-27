using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using SiberMailer.Data.Repositories;

namespace SiberMailer.Business.Services;

/// <summary>
/// Service for authentication and session management.
/// Uses a singleton-like pattern for current user session.
/// </summary>
public class AuthService
{
    private readonly UserRepository _userRepository;
    
    // Current logged-in user (null if not logged in)
    private User? _currentUser;
    private DateTime? _loginTime;

    /// <summary>
    /// Event raised when login state changes.
    /// </summary>
    public event EventHandler<AuthEventArgs>? AuthStateChanged;

    public AuthService(UserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    #region Properties

    /// <summary>
    /// Gets the currently logged-in user.
    /// </summary>
    public User? CurrentUser => _currentUser;

    /// <summary>
    /// Returns true if a user is currently logged in.
    /// </summary>
    public bool IsLoggedIn => _currentUser != null;

    /// <summary>
    /// Gets the login timestamp.
    /// </summary>
    public DateTime? LoginTime => _loginTime;

    /// <summary>
    /// Gets the current user's role, or null if not logged in.
    /// </summary>
    public UserRole? CurrentUserRole => _currentUser?.Role;

    /// <summary>
    /// Gets the current user's ID, or 0 if not logged in.
    /// </summary>
    public int CurrentUserId => _currentUser?.UserId ?? 0;

    #endregion

    #region Authentication Methods

    /// <summary>
    /// Attempts to log in with the provided credentials.
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Plain text password</param>
    /// <returns>Login result with success status and message</returns>
    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            return LoginResult.Failure("Username is required");

        if (string.IsNullOrWhiteSpace(password))
            return LoginResult.Failure("Password is required");

        try
        {
            // Attempt login through repository (handles hash comparison)
            var user = await _userRepository.LoginAsync(username, password);

            if (user == null)
            {
                // Check if user exists to give more specific error
                var existingUser = await _userRepository.GetByUsernameAsync(username);
                
                if (existingUser == null)
                    return LoginResult.Failure("Invalid username or password");
                
                if (!existingUser.IsActive)
                    return LoginResult.Failure("Account is deactivated");
                
                return LoginResult.Failure("Invalid username or password");
            }

            // Successful login
            _currentUser = user;
            _loginTime = DateTime.UtcNow;

            OnAuthStateChanged(new AuthEventArgs(AuthEvent.LoggedIn, user));

            return LoginResult.Success(user);
        }
        catch (Exception ex)
        {
            return LoginResult.Failure($"Login failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    public void Logout()
    {
        var previousUser = _currentUser;
        _currentUser = null;
        _loginTime = null;

        if (previousUser != null)
        {
            OnAuthStateChanged(new AuthEventArgs(AuthEvent.LoggedOut, previousUser));
        }
    }

    /// <summary>
    /// Changes the password for the current user.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (_currentUser == null)
            throw new InvalidOperationException("No user is logged in");

        // Verify current password
        var verifyUser = await _userRepository.LoginAsync(_currentUser.Username, currentPassword);
        if (verifyUser == null)
            return false;

        // TODO: Implement password change in UserRepository
        // For now, this is a placeholder
        throw new NotImplementedException("Password change not yet implemented in repository");
    }

    #endregion

    #region Authorization Methods

    /// <summary>
    /// Checks if the current user has the required role.
    /// </summary>
    public bool HasRole(UserRole requiredRole)
    {
        if (_currentUser == null)
            return false;

        // Admin has all roles
        if (_currentUser.Role == UserRole.Admin)
            return true;

        // Manager has Manager and Member roles
        if (_currentUser.Role == UserRole.Manager && requiredRole != UserRole.Admin)
            return true;

        return _currentUser.Role == requiredRole;
    }

    /// <summary>
    /// Checks if the current user is an Admin.
    /// </summary>
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    /// <summary>
    /// Checks if the current user is a Manager or higher.
    /// </summary>
    public bool IsManagerOrHigher => _currentUser?.Role == UserRole.Admin || 
                                      _currentUser?.Role == UserRole.Manager;

    /// <summary>
    /// Throws an exception if the user doesn't have the required role.
    /// </summary>
    public void RequireRole(UserRole requiredRole)
    {
        if (!HasRole(requiredRole))
        {
            throw new UnauthorizedAccessException(
                $"This action requires {requiredRole} role. Current role: {_currentUser?.Role}");
        }
    }

    /// <summary>
    /// Throws an exception if no user is logged in.
    /// </summary>
    public void RequireAuthentication()
    {
        if (!IsLoggedIn)
        {
            throw new UnauthorizedAccessException("Authentication required");
        }
    }

    #endregion

    #region Events

    protected virtual void OnAuthStateChanged(AuthEventArgs e)
    {
        AuthStateChanged?.Invoke(this, e);
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Result of a login attempt.
/// </summary>
public class LoginResult
{
    public bool IsSuccess { get; private set; }
    public User? User { get; private set; }
    public string? ErrorMessage { get; private set; }

    private LoginResult() { }

    public static LoginResult Success(User user) => new()
    {
        IsSuccess = true,
        User = user
    };

    public static LoginResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}

/// <summary>
/// Authentication event types.
/// </summary>
public enum AuthEvent
{
    LoggedIn,
    LoggedOut,
    SessionExpired
}

/// <summary>
/// Event arguments for authentication state changes.
/// </summary>
public class AuthEventArgs : EventArgs
{
    public AuthEvent Event { get; }
    public User? User { get; }

    public AuthEventArgs(AuthEvent authEvent, User? user = null)
    {
        Event = authEvent;
        User = user;
    }
}

#endregion
