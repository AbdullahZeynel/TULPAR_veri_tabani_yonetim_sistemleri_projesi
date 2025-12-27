using Dapper;
using SiberMailer.Core.Models;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for Template-related database operations.
/// </summary>
public class TemplateRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TemplateRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets all templates with branch names.
    /// </summary>
    public async Task<IEnumerable<Template>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                t.TemplateId,
                t.TemplateName,
                t.htmlbody AS HtmlContent,
                t.BranchId,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                b.BranchName
            FROM Templates t
            LEFT JOIN Branches b ON t.BranchId = b.BranchId
            ORDER BY t.TemplateName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Template>(sql);
    }

    /// <summary>
    /// Gets active templates only.
    /// </summary>
    public async Task<IEnumerable<Template>> GetActiveAsync()
    {
        const string sql = @"
            SELECT 
                t.TemplateId,
                t.TemplateName,
                t.htmlbody AS HtmlContent,
                t.BranchId,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                b.BranchName
            FROM Templates t
            LEFT JOIN Branches b ON t.BranchId = b.BranchId
            WHERE t.IsActive = TRUE
            ORDER BY t.TemplateName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Template>(sql);
    }

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    public async Task<Template?> GetByIdAsync(int templateId)
    {
        const string sql = @"
            SELECT 
                t.TemplateId,
                t.TemplateName,
                t.htmlbody AS HtmlContent,
                t.BranchId,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                b.BranchName
            FROM Templates t
            LEFT JOIN Branches b ON t.BranchId = b.BranchId
            WHERE t.TemplateId = @TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<Template>(sql, new { TemplateId = templateId });
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    public async Task<int> CreateAsync(Template template)
    {
        const string sql = @"
            INSERT INTO Templates (TemplateName, htmlbody, BranchId, IsActive, CreatedAt, UpdatedAt)
            VALUES (@TemplateName, @HtmlContent, @BranchId, @IsActive, NOW(), NOW())
            RETURNING TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>(sql, new
        {
            template.TemplateName,
            template.HtmlContent,
            template.BranchId,
            template.IsActive
        });
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    public async Task<bool> UpdateAsync(Template template)
    {
        const string sql = @"
            UPDATE Templates
            SET TemplateName = @TemplateName,
                htmlbody = @HtmlContent,
                BranchId = @BranchId,
                IsActive = @IsActive,
                UpdatedAt = NOW()
            WHERE TemplateId = @TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            template.TemplateId,
            template.TemplateName,
            template.HtmlContent,
            template.BranchId,
            template.IsActive
        });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Soft deletes a template by setting IsActive to false.
    /// </summary>
    public async Task<bool> DeactivateAsync(int templateId)
    {
        const string sql = @"
            UPDATE Templates
            SET IsActive = FALSE, UpdatedAt = NOW()
            WHERE TemplateId = @TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { TemplateId = templateId });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Hard deletes a template from the database.
    /// </summary>
    public async Task<bool> DeleteAsync(int templateId)
    {
        const string sql = "DELETE FROM Templates WHERE TemplateId = @TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { TemplateId = templateId });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Toggles the IsActive status of a template.
    /// </summary>
    public async Task<bool> ToggleStatusAsync(int templateId)
    {
        const string sql = @"
            UPDATE Templates
            SET IsActive = NOT IsActive, UpdatedAt = NOW()
            WHERE TemplateId = @TemplateId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { TemplateId = templateId });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Gets templates by branch ID.
    /// </summary>
    public async Task<IEnumerable<Template>> GetByBranchAsync(int branchId)
    {
        const string sql = @"
            SELECT 
                t.TemplateId,
                t.TemplateName,
                t.htmlbody AS HtmlContent,
                t.BranchId,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                b.BranchName
            FROM Templates t
            LEFT JOIN Branches b ON t.BranchId = b.BranchId
            WHERE t.BranchId = @BranchId
            ORDER BY t.TemplateName";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Template>(sql, new { BranchId = branchId });
    }
}
