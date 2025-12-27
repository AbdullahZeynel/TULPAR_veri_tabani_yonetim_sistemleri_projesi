using Dapper;
using SiberMailer.Core.Models;
using SiberMailer.Core.Enums;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for MailJob database operations using sp_create_job.
/// </summary>
public class MailJobRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public MailJobRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Creates a new mail job using sp_create_job stored procedure.
    /// </summary>
    public async Task<CreateJobResult> CreateJobAsync(CreateJobDto job)
    {
        const string sql = @"
            INSERT INTO MailJobs (
                ListId, SmtpAccountId, Subject, HtmlBody, PlainTextBody,
                CreatedByUserId, Status, RequiresApproval, CreatedAt, UpdatedAt
            )
            VALUES (
                @ListId, @SmtpAccountId, @Subject, @HtmlBody, @PlainTextBody,
                @CreatedByUserId, 'Approved'::job_status, false, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
            )
            RETURNING JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        
        try
        {
            var jobId = await connection.QuerySingleAsync<int>(sql, new
            {
                job.ListId,
                job.SmtpAccountId,
                job.Subject,
                job.HtmlBody,
                job.PlainTextBody,
                job.CreatedByUserId
            });

            return new CreateJobResult
            {
                Success = true,
                JobId = jobId,
                Message = "Campaign created and queued for immediate sending"
            };
        }
        catch (Exception ex)
        {
            return new CreateJobResult
            {
                Success = false,
                Message = $"Error creating campaign: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets jobs by status.
    /// </summary>
    public async Task<IEnumerable<MailJob>> GetJobsByStatusAsync(JobStatus status)
    {
        const string sql = @"
            SELECT j.*, u.FullName as CreatedByName
            FROM MailJobs j
            LEFT JOIN Users u ON j.CreatedByUserId = u.UserId
            WHERE j.Status = @Status::job_status
            ORDER BY j.CreatedAt DESC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<MailJob>(sql, new { Status = status.ToString() });
    }

    /// <summary>
    /// Gets pending jobs for approval.
    /// </summary>
    public async Task<IEnumerable<MailJob>> GetPendingJobsAsync()
    {
        return await GetJobsByStatusAsync(JobStatus.PendingApproval);
    }

    /// <summary>
    /// Approves a pending job (Admin only).
    /// </summary>
    public async Task<bool> ApproveJobAsync(int jobId, int approvedByUserId)
    {
        const string sql = @"
            UPDATE MailJobs 
            SET Status = 'Approved', 
                ApprovedByUserId = @ApprovedByUserId,
                ApprovedAt = NOW()
            WHERE JobId = @JobId AND Status = 'Pending'
            RETURNING JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { JobId = jobId, ApprovedByUserId = approvedByUserId });
        return result.HasValue;
    }

    /// <summary>
    /// Rejects a pending job.
    /// </summary>
    public async Task<bool> RejectJobAsync(int jobId, int rejectedByUserId, string reason)
    {
        const string sql = @"
            UPDATE MailJobs 
            SET Status = 'Rejected',
                ApprovedByUserId = @RejectedByUserId,
                ApprovedAt = NOW()
            WHERE JobId = @JobId AND Status = 'Pending'
            RETURNING JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { JobId = jobId, RejectedByUserId = rejectedByUserId });
        return result.HasValue;
    }
}

/// <summary>
/// DTO for creating a mail job.
/// </summary>
public class CreateJobDto
{
    public int ListId { get; set; }
    public int SmtpAccountId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    
    public int CreatedByUserId { get; set; }
    
    // New fields for Campaign Wizard enhancement
    public string? EmailSubject { get; set; }
    public string? AdminNotes { get; set; }
    public string? AttachmentPaths { get; set; }
}

/// <summary>
/// Result from sp_create_job.
/// </summary>
public class CreateJobResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? JobId { get; set; }
    public int? RecipientCount { get; set; }
}
