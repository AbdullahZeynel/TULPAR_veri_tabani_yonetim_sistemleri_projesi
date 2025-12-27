using Dapper;
using SiberMailer.Core.Models;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for RecipientList database operations.
/// </summary>
public class RecipientListRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public RecipientListRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets all recipient lists.
    /// </summary>
    public async Task<IEnumerable<RecipientList>> GetAllAsync()
    {
        const string sql = @"
            SELECT ListId, ListName, Description, BranchId, CreatedAt, UpdatedAt
            FROM RecipientLists
            ORDER BY ListName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<RecipientList>(sql);
    }

    /// <summary>
    /// Gets recipient lists for a specific branch.
    /// </summary>
    public async Task<IEnumerable<RecipientList>> GetByBranchAsync(int branchId)
    {
        const string sql = @"
            SELECT ListId, ListName, Description, BranchId, CreatedAt, UpdatedAt
            FROM RecipientLists
            WHERE BranchId = @BranchId
            ORDER BY ListName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<RecipientList>(sql, new { BranchId = branchId });
    }

    /// <summary>
    /// Gets a recipient list by ID.
    /// </summary>
    public async Task<RecipientList?> GetByIdAsync(int listId)
    {
        const string sql = @"
            SELECT ListId, ListName, Description, BranchId, CreatedAt, UpdatedAt
            FROM RecipientLists
            WHERE ListId = @ListId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<RecipientList>(sql, new { ListId = listId });
    }

    /// <summary>
    /// Gets list members using fn_get_list_members stored function.
    /// </summary>
    public async Task<IEnumerable<Contact>> GetListMembersAsync(int listId, string? statusFilter = null)
    {
        const string sql = "SELECT * FROM fn_get_list_members(@ListId, @StatusFilter)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Contact>(sql, new { ListId = listId, StatusFilter = statusFilter });
    }

    /// <summary>
    /// Gets lists with contact counts.
    /// </summary>
    public async Task<IEnumerable<RecipientListWithCount>> GetAllWithCountsAsync()
    {
        const string sql = @"
            SELECT 
                r.ListId, r.ListName, r.Description, r.BranchId, r.CreatedAt, r.UpdatedAt,
                b.BranchName,
                TRUE as IsActive,
                COUNT(c.ContactId) AS ContactCount,
                COUNT(CASE WHEN c.Status = 'Active' THEN 1 END) AS ActiveCount
            FROM RecipientLists r
            LEFT JOIN Branches b ON r.BranchId = b.BranchId
            LEFT JOIN Contacts c ON r.ListId = c.ListId
            GROUP BY r.ListId, r.ListName, r.Description, r.BranchId, r.CreatedAt, r.UpdatedAt, b.BranchName
            ORDER BY r.ListName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<RecipientListWithCount>(sql);
    }

    /// <summary>
    /// Creates a new recipient list.
    /// </summary>
    public async Task<int> CreateAsync(RecipientList list)
    {
        const string sql = @"
            INSERT INTO RecipientLists (ListName, Description, BranchId)
            VALUES (@ListName, @Description, @BranchId)
            RETURNING ListId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>(sql, list);
    }

    /// <summary>
    /// Deletes a recipient list by ID.
    /// Note: This does not delete contacts - they remain orphaned.
    /// </summary>
    public async Task DeleteAsync(int listId)
    {
        const string sql = "DELETE FROM RecipientLists WHERE ListId = @ListId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { ListId = listId });
    }
}

/// <summary>
/// RecipientList entity matching the PostgreSQL 'RecipientLists' table.
/// </summary>
public class RecipientList
{
    public int ListId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// RecipientList with contact count.
/// </summary>
public class RecipientListWithCount : RecipientList
{
    public int ContactCount { get; set; }
    public int ActiveCount { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; } = true;
}
