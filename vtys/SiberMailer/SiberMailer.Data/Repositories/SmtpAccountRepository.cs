using Dapper;
using SiberMailer.Core.Models;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for SmtpAccount (The Vault) database operations.
/// </summary>
public class SmtpAccountRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public SmtpAccountRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Creates a new SMTP account with encrypted credentials.
    /// </summary>
    public async Task<int> CreateAsync(SmtpAccount account)
    {
        const string sql = @"
            INSERT INTO SmtpAccounts (
                AccountName, SmtpHost, SmtpPort, UseSsl,
                Email, EncryptedPassword, EncryptionIV,
                DailyLimit, OwnerBranchId, IsShared
            ) VALUES (
                @AccountName, @SmtpHost, @SmtpPort, @UseSsl,
                @Email, @EncryptedPassword, @EncryptionIV,
                @DailyLimit, @OwnerBranchId, @IsShared
            ) RETURNING SmtpAccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>(sql, account);
    }

    /// <summary>
    /// Gets an SMTP account by ID.
    /// </summary>
    public async Task<SmtpAccount?> GetByIdAsync(int accountId)
    {
        const string sql = @"
            SELECT SmtpAccountId, AccountName, SmtpHost, SmtpPort, UseSsl,
                   Email, EncryptedPassword, EncryptionIV,
                   DailyLimit, SentToday, LastResetDate,
                   OwnerBranchId, IsShared, IsActive, CreatedAt, UpdatedAt
            FROM SmtpAccounts 
            WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<SmtpAccount>(sql, new { AccountId = accountId });
    }

    /// <summary>
    /// Gets an SMTP account by name.
    /// </summary>
    public async Task<SmtpAccount?> GetByNameAsync(string accountName)
    {
        const string sql = @"
            SELECT SmtpAccountId, AccountName, SmtpHost, SmtpPort, UseSsl,
                   Email, EncryptedPassword, EncryptionIV,
                   DailyLimit, SentToday, LastResetDate,
                   OwnerBranchId, IsShared, IsActive, CreatedAt, UpdatedAt
            FROM SmtpAccounts 
            WHERE AccountName = @AccountName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<SmtpAccount>(sql, new { AccountName = accountName });
    }

    /// <summary>
    /// Gets all active SMTP accounts accessible by a user.
    /// </summary>
    public async Task<IEnumerable<SmtpAccount>> GetAccessibleByUserAsync(int userId)
    {
        const string sql = @"
            SELECT SmtpAccountId, AccountName, SmtpHost, SmtpPort, UseSsl,
                   Email, EncryptedPassword, EncryptionIV,
                   DailyLimit, SentToday, LastResetDate,
                   OwnerBranchId, IsShared, IsActive, CreatedAt, UpdatedAt
            FROM SmtpAccounts 
            WHERE IsActive = TRUE 
              AND (OwnerBranchId = @BranchId OR IsShared = TRUE)
            ORDER BY AccountName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<SmtpAccount>(sql, new { BranchId = userId });
    }

    /// <summary>
    /// Gets all SMTP accounts.
    /// </summary>
    public async Task<IEnumerable<SmtpAccount>> GetAllAsync(bool activeOnly = true)
    {
        var sql = @"
            SELECT SmtpAccountId, AccountName, SmtpHost, SmtpPort, UseSsl,
                   Email, EncryptedPassword, EncryptionIV,
                   DailyLimit, SentToday, LastResetDate,
                   OwnerBranchId, IsShared, IsActive, CreatedAt, UpdatedAt
            FROM SmtpAccounts" +
            (activeOnly ? " WHERE IsActive = TRUE" : "") + @"
            ORDER BY AccountName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<SmtpAccount>(sql);
    }

    /// <summary>
    /// Gets all SMTP accounts with statistics and audit info from the view.
    /// </summary>
    public async Task<IEnumerable<SmtpAccount>> GetAllWithStatsAsync(bool activeOnly = false)
    {
        var sql = @"
            SELECT SmtpAccountId, AccountName, SmtpHost, SmtpPort, UseSsl,
                   DailyLimit, SentToday, LastResetDate,
                   OwnerBranchId, IsShared, IsActive, CreatedAt, UpdatedAt,
                   CreatedByUserId, UpdatedByUserId,
                   CreatedByName, UpdatedByName,
                   TotalSentCount, TodaySentCount, LastSentAt
            FROM vw_SmtpAccountStats" +
            (activeOnly ? " WHERE IsActive = TRUE" : "") + @"
            ORDER BY AccountName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<SmtpAccount>(sql);
    }

    /// <summary>
    /// Toggles the IsActive status of an SMTP account.
    /// </summary>
    public async Task<bool> ToggleStatusAsync(int accountId)
    {
        const string sql = @"
            UPDATE SmtpAccounts 
            SET IsActive = NOT IsActive, UpdatedAt = CURRENT_TIMESTAMP
            WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { AccountId = accountId });
        return affected > 0;
    }

    /// <summary>
    /// Increments the sent count for rate limiting.
    /// Resets count if it's a new day.
    /// </summary>
    public async Task IncrementSentCountAsync(int accountId, int count = 1)
    {
        const string sql = @"
            UPDATE SmtpAccounts 
            SET SentToday = CASE 
                    WHEN LastResetDate < CURRENT_DATE THEN @Count 
                    ELSE SentToday + @Count 
                END,
                LastResetDate = CURRENT_DATE,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { AccountId = accountId, Count = count });
    }

    /// <summary>
    /// Updates an SMTP account (non-credential fields only).
    /// </summary>
    public async Task<bool> UpdateAsync(SmtpAccount account)
    {
        const string sql = @"
            UPDATE SmtpAccounts 
            SET AccountName = @AccountName,
                SmtpHost = @SmtpHost,
                SmtpPort = @SmtpPort,
                UseSsl = @UseSsl,
                DailyLimit = @DailyLimit,
                IsShared = @IsShared,
                IsActive = @IsActive,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SmtpAccountId = @SmtpAccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, account);
        return affected > 0;
    }

    /// <summary>
    /// Updates encrypted credentials.
    /// </summary>
    public async Task<bool> UpdateCredentialsAsync(int accountId, string email, string encryptedPassword, string iv)
    {
        const string sql = @"
            UPDATE SmtpAccounts 
            SET Email = @Email,
                EncryptedPassword = @EncryptedPassword,
                EncryptionIV = @EncryptionIV,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new 
        { 
            AccountId = accountId, 
            Email = email, 
            EncryptedPassword = encryptedPassword, 
            EncryptionIV = iv 
        });
        return affected > 0;
    }

    /// <summary>
    /// Deactivates an SMTP account (soft delete).
    /// </summary>
    public async Task<bool> DeactivateAsync(int accountId)
    {
        const string sql = @"
            UPDATE SmtpAccounts 
            SET IsActive = FALSE, UpdatedAt = CURRENT_TIMESTAMP
            WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { AccountId = accountId });
        return affected > 0;
    }

    /// <summary>
    /// Permanently deletes an SMTP account (hard delete).
    /// </summary>
    public async Task<bool> DeleteAsync(int accountId)
    {
        const string sql = "DELETE FROM SmtpAccounts WHERE SmtpAccountId = @AccountId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { AccountId = accountId });
        return affected > 0;
    }
}
