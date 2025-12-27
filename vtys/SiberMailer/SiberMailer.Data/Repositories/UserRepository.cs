using Dapper;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for User entity database operations.
/// </summary>
public class UserRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UserRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets a user by their username.
    /// </summary>
    /// <param name="username">The username to search for</param>
    /// <returns>User if found, null otherwise</returns>
    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = @"
            SELECT UserId, Username, Email, PasswordHash, 
                   FullName, Role, IsActive, LastLoginAt, 
                   CreatedAt, UpdatedAt
            FROM Users 
            WHERE Username = @Username";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();

        // Dapper needs custom type handler for enum
        var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        return user;
    }

    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    /// <param name="email">The email to search for</param>
    /// <returns>User if found, null otherwise</returns>
    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = @"
            SELECT UserId, Username, Email, PasswordHash, 
                   FullName, Role, IsActive, LastLoginAt, 
                   CreatedAt, UpdatedAt
            FROM Users 
            WHERE Email = @Email";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>User if found, null otherwise</returns>
    public async Task<User?> GetByIdAsync(int userId)
    {
        const string sql = @"
            SELECT UserId, Username, Email, PasswordHash, 
                   FullName, Role, IsActive, LastLoginAt, 
                   CreatedAt, UpdatedAt
            FROM Users 
            WHERE UserId = @UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
    }

    /// <summary>
    /// Attempts to authenticate a user with username and password.
    /// Returns the User if successful, null if authentication fails.
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Plain text password</param>
    /// <returns>Authenticated User or null</returns>
    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await GetByUsernameAsync(username);

        // User not found
        if (user == null)
            return null;

        // Account is inactive
        if (!user.IsActive)
            return null;

        // Hash the provided password and compare
        var passwordHash = HashPassword(password);

        if (user.PasswordHash != passwordHash)
        {
            return null;
        }

        // Successful login - update last login time
        await UpdateLastLoginAsync(user.UserId);
        user.LastLoginAt = DateTime.UtcNow;

        return user;
    }

    /// <summary>
    /// Updates the last login timestamp for a user.
    /// </summary>
    private async Task UpdateLastLoginAsync(int userId)
    {
        const string sql = @"
            UPDATE Users 
            SET LastLoginAt = CURRENT_TIMESTAMP
            WHERE UserId = @UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }



    /// <summary>
    /// Gets all active users.
    /// </summary>
    public async Task<IEnumerable<User>> GetAllActiveAsync()
    {
        const string sql = @"
            SELECT UserId, Username, Email, PasswordHash, 
                   FullName, Role, IsActive, LastLoginAt, 
                   CreatedAt, UpdatedAt
            FROM Users 
            WHERE IsActive = TRUE
            ORDER BY FullName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<User>(sql);
    }

    /// <summary>
    /// Hashes a password using SHA-256.
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <returns>Hexadecimal hash string</returns>
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    /// <summary>
    /// Updates the password for a specific user.
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash)
    {
        const string sql = "UPDATE Users SET PasswordHash = @PasswordHash, UpdatedAt = CURRENT_TIMESTAMP WHERE UserId = @UserId";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, PasswordHash = newPasswordHash });
        return rowsAffected > 0;
    }

    #region Admin CRUD Operations

    /// <summary>
    /// Creates a new user (Admin only).
    /// </summary>
    public async Task<int> CreateUserAsync(User user)
    {
        const string sql = @"
            INSERT INTO Users (Username, Email, PasswordHash, FullName, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Username, @Email, @PasswordHash, @FullName, @Role::user_role, @IsActive, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(sql, new 
        { 
            user.Username, 
            user.Email, 
            user.PasswordHash, 
            user.FullName, 
            Role = user.Role.ToString(), 
            user.IsActive 
        });
    }

    /// <summary>
    /// Updates an existing user (Admin only).
    /// </summary>
    public async Task<bool> UpdateUserAsync(User user)
    {
        const string sql = @"
            UPDATE Users 
            SET Username = @Username,
                Email = @Email,
                FullName = @FullName,
                Role = @Role::user_role,
                IsActive = @IsActive,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE UserId = @UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new 
        { 
            user.Username, 
            user.Email, 
            user.FullName, 
            Role = user.Role.ToString(), 
            user.IsActive,
            user.UserId
        });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Soft deletes a user by setting IsActive to false (Admin only).
    /// </summary>
    public async Task<bool> DeleteUserAsync(int userId)
    {
        const string sql = "UPDATE Users SET IsActive = FALSE, UpdatedAt = CURRENT_TIMESTAMP WHERE UserId = @UserId";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId });
        return rowsAffected > 0;
    }

    /// <summary>
    /// Permanently deletes a user from the database (Admin only - use with caution).
    /// </summary>
    public async Task<bool> HardDeleteUserAsync(int userId)
    {
        const string sql = "DELETE FROM Users WHERE UserId = @UserId";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId });
        return rowsAffected > 0;
    }

    /// <summary>
    /// Gets all users, including inactive ones (Admin only).
    /// </summary>
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        const string sql = @"
            SELECT UserId, Username, Email, PasswordHash, 
                   FullName, Role, IsActive, LastLoginAt, 
                   CreatedAt, UpdatedAt
            FROM Users 
            ORDER BY UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<User>(sql);
    }

    #endregion
}
