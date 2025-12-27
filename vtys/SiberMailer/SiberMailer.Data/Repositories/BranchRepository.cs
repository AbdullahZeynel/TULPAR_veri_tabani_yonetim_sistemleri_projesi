using Dapper;
using SiberMailer.Core.Models;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for Branch-related database operations.
/// </summary>
public class BranchRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public BranchRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets statistics for a specific branch using fn_get_branch_stats.
    /// </summary>
    public async Task<BranchStats?> GetBranchStatsAsync(int branchId)
    {
        const string sql = "SELECT fn_get_branch_stats(@BranchId) AS stats_json";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var json = await connection.QueryFirstOrDefaultAsync<string>(sql, new { BranchId = branchId });

        if (string.IsNullOrEmpty(json))
            return null;

        return Newtonsoft.Json.JsonConvert.DeserializeObject<BranchStats>(json);
    }

    /// <summary>
    /// Gets aggregated stats for all branches (for Admin dashboard).
    /// </summary>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        const string sql = @"
            SELECT 
                (SELECT COUNT(*) FROM Contacts WHERE Status = 'Active') AS TotalActiveContacts,
                (SELECT COUNT(*) FROM Contacts) AS TotalContacts,
                (SELECT COUNT(*) FROM RecipientLists) AS TotalLists,
                (SELECT COUNT(*) FROM MailJobs) AS TotalCampaigns,
                (SELECT COUNT(*) FROM MailJobs WHERE Status = 'Completed') AS CompletedCampaigns,
                (SELECT COUNT(*) FROM MailJobs WHERE Status = 'Processing') AS ActiveCampaigns,
                (SELECT COUNT(*) FROM MailJobLogs WHERE Status = 'Sent') AS TotalEmailsSent,
                (SELECT COUNT(*) FROM Templates) AS TotalTemplates,
                (SELECT COUNT(*) FROM SmtpAccounts WHERE IsActive = TRUE) AS ActiveSmtpAccounts,
                (SELECT COUNT(*) FROM Contacts WHERE Status = 'Bounced') AS BouncedContacts,
                (SELECT COUNT(*) FROM Contacts WHERE Status = 'RedListed') AS RedListedContacts,
                (SELECT COUNT(*) FROM Users WHERE IsActive = TRUE) AS ActiveUsers,
                (SELECT COUNT(*) FROM MailJobLogs WHERE Status = 'Sent' AND DATE(SentAt) = CURRENT_DATE) AS SentToday";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstAsync<DashboardStats>(sql);
    }

    /// <summary>
    /// Gets all branches.
    /// </summary>
    public async Task<IEnumerable<Branch>> GetAllAsync()
    {
        const string sql = @"
            SELECT BranchId, BranchCode, BranchName, Description, IsActive, CreatedAt, UpdatedAt
            FROM Branches
            WHERE IsActive = TRUE
            ORDER BY BranchName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Branch>(sql);
    }

    #region Admin CRUD Operations

    /// <summary>
    /// Creates a new branch (Admin only).
    /// </summary>
    public async Task<int> CreateBranchAsync(Branch branch)
    {
        const string sql = @"
            INSERT INTO Branches (BranchCode, BranchName, Description, IsActive, CreatedAt, UpdatedAt)
            VALUES (@BranchCode, @BranchName, @Description, @IsActive, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING BranchId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(sql, new 
        { 
            branch.BranchCode, 
            branch.BranchName, 
            branch.Description, 
            branch.IsActive 
        });
    }

    /// <summary>
    /// Updates an existing branch (Admin only).
    /// </summary>
    public async Task<bool> UpdateBranchAsync(Branch branch)
    {
        const string sql = @"
            UPDATE Branches 
            SET BranchCode = @BranchCode,
                BranchName = @BranchName,
                Description = @Description,
                IsActive = @IsActive,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE BranchId = @BranchId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new 
        { 
            branch.BranchCode, 
            branch.BranchName, 
            branch.Description, 
            branch.IsActive, 
            branch.BranchId
        });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Soft deletes a branch by setting IsActive to false (Admin only).
    /// </summary>
    public async Task<bool> DeleteBranchAsync(int branchId)
    {
        const string sql = "UPDATE Branches SET IsActive = FALSE, UpdatedAt = CURRENT_TIMESTAMP WHERE BranchId = @BranchId";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { BranchId = branchId });
        return rowsAffected > 0;
    }

    /// <summary>
    /// Permanently deletes a branch from the database (Admin only - use with caution).
    /// </summary>
    public async Task<bool> HardDeleteBranchAsync(int branchId)
    {
        const string sql = "DELETE FROM Branches WHERE BranchId = @BranchId";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { BranchId = branchId });
        return rowsAffected > 0;
    }

    /// <summary>
    /// Gets all branches, including inactive ones (Admin only).
    /// </summary>
    public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
    {
        const string sql = @"
            SELECT BranchId, BranchCode, BranchName, Description, IsActive, CreatedAt, UpdatedAt
            FROM Branches
            ORDER BY BranchName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Branch>(sql);
    }

    #endregion
}

/// <summary>
/// Branch entity matching the PostgreSQL 'Branches' table.
/// </summary>
public class Branch
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Statistics for a single branch from fn_get_branch_stats.
/// </summary>
public class BranchStats
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int TotalLists { get; set; }
    public int TotalContacts { get; set; }
    public int ActiveContacts { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
}

/// <summary>
/// Aggregated dashboard statistics.
/// </summary>
public class DashboardStats
{
    public int TotalContacts { get; set; }
    public int TotalActiveContacts { get; set; }
    public int TotalLists { get; set; }
    public int TotalCampaigns { get; set; }
    public int CompletedCampaigns { get; set; }
    public int ActiveCampaigns { get; set; }
    public long TotalEmailsSent { get; set; }
    public int TotalTemplates { get; set; }
    public int ActiveSmtpAccounts { get; set; }
    public int BouncedContacts { get; set; }
    public int RedListedContacts { get; set; }
    public int ActiveUsers { get; set; }
    public int SentToday { get; set; }
}
