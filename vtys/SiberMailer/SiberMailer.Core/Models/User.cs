using SiberMailer.Core.Enums;

namespace SiberMailer.Core.Models;

/// <summary>
/// User entity matching the PostgreSQL 'Users' table.
/// </summary>
public class User
{
    /// <summary>Primary key</summary>
    public int UserId { get; set; }

    /// <summary>Unique username for login</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Unique email address</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 hashed password</summary>
    public string PasswordHash { get; set; } = string.Empty;



    /// <summary>Display name</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>User role: Admin, Manager, or Member</summary>
    public UserRole Role { get; set; } = UserRole.Member;

    /// <summary>Whether the user account is active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp of last successful login</summary>
    public DateTime? LastLoginAt { get; set; }



    /// <summary>Record creation timestamp</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp</summary>
    public DateTime UpdatedAt { get; set; }



    /// <summary>
    /// Returns a display-friendly string representation.
    /// </summary>
    public override string ToString() => $"{FullName} ({Username}) - {Role}";
}
