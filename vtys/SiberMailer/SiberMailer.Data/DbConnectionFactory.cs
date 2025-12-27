using Npgsql;
using System.Data;

namespace SiberMailer.Data;

/// <summary>
/// Factory class for creating PostgreSQL database connections.
/// Centralizes connection string management and ensures consistent connection handling.
/// </summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes the factory with the PostgreSQL connection string.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    public DbConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
        
        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates a new PostgreSQL connection.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    /// <returns>A new NpgsqlConnection instance</returns>
    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    /// <summary>
    /// Creates and opens a new PostgreSQL connection.
    /// Use this for immediate database operations.
    /// </summary>
    /// <returns>An open NpgsqlConnection instance</returns>
    public IDbConnection CreateOpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Creates and opens a new PostgreSQL connection asynchronously.
    /// Preferred method for async database operations.
    /// </summary>
    /// <returns>An open NpgsqlConnection instance</returns>
    public async Task<IDbConnection> CreateOpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Gets the connection string (for diagnostics only).
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Default connection string for local development.
    /// In production, this should come from configuration/environment variables.
    /// </summary>
    public static string DefaultConnectionString => 
        "Host=localhost;Port=5432;Database=siber_mailer;Username=postgres;Password=godgod";
}
