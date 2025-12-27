using Dapper;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using System.Data;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for MailJob and MailJobLog database operations.
/// </summary>
public class JobRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public JobRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    #region MailJob Operations

    /// <summary>
    /// Creates a new mail job using the sp_create_mail_job stored procedure.
    /// </summary>
    /// <param name="dto">Job creation data</param>
    /// <returns>The created job ID</returns>
    public async Task<int> CreateJobAsync(CreateMailJobDto dto)
    {
        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        // Call the stored procedure: sp_create_mail_job
        await using var cmd = new NpgsqlCommand(@"
            CALL sp_create_mail_job(
                @p_job_name, @p_branch_id, @p_list_id, @p_template_id, 
                @p_smtp_account_id, @p_created_by, @p_job_id, @p_requires_approval
            )", connection);

        cmd.Parameters.AddWithValue("p_job_name", dto.JobName);
        cmd.Parameters.AddWithValue("p_branch_id", dto.BranchId);
        cmd.Parameters.AddWithValue("p_list_id", dto.ListId);
        cmd.Parameters.AddWithValue("p_template_id", dto.TemplateId);
        cmd.Parameters.AddWithValue("p_smtp_account_id", dto.SmtpAccountId);
        cmd.Parameters.AddWithValue("p_created_by", dto.CreatedBy);
        cmd.Parameters.Add(new NpgsqlParameter("p_job_id", NpgsqlDbType.Integer) { Direction = ParameterDirection.Output });
        cmd.Parameters.AddWithValue("p_requires_approval", dto.RequiresApproval);

        await cmd.ExecuteNonQueryAsync();

        return (int)cmd.Parameters["p_job_id"].Value!;
    }

    /// <summary>
    /// Gets a mail job by ID.
    /// </summary>
    public async Task<MailJob?> GetByIdAsync(int jobId)
    {
        const string sql = @"
            SELECT JobId, JobName, BranchId, ListId, TemplateId, SmtpAccountId,
                   Status, TotalRecipients,
                   StartedAt, CompletedAt,
                   RequiresApproval, ApprovedBy, ApprovedAt, RejectionReason,
                   CreatedBy, CreatedAt, UpdatedAt
            FROM MailJobs 
            WHERE JobId = @JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<MailJob>(sql, new { JobId = jobId });
    }

    /// <summary>
    /// Gets all jobs for a branch.
    /// </summary>
    public async Task<IEnumerable<MailJob>> GetByBranchAsync(int branchId, JobStatus? filterStatus = null)
    {
        var sql = @"
            SELECT JobId, JobName, BranchId, ListId, TemplateId, SmtpAccountId,
                   Status, TotalRecipients,
                   StartedAt, CompletedAt,
                   RequiresApproval, ApprovedBy, ApprovedAt, RejectionReason,
                   CreatedBy, CreatedAt, UpdatedAt
            FROM MailJobs 
            WHERE BranchId = @BranchId" + 
            (filterStatus.HasValue ? " AND Status = @Status::job_status" : "") + @"
            ORDER BY CreatedAt DESC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<MailJob>(sql, new { BranchId = branchId, Status = filterStatus?.ToString() });
    }

    /// <summary>
    /// Gets all jobs pending approval.
    /// </summary>
    public async Task<IEnumerable<MailJob>> GetPendingApprovalAsync()
    {
        const string sql = @"
            SELECT JobId, JobName, BranchId, ListId, TemplateId, SmtpAccountId,
                   Status, TotalRecipients,
                   StartedAt, CompletedAt,
                   RequiresApproval, ApprovedBy, ApprovedAt, RejectionReason,
                   CreatedBy, CreatedAt, UpdatedAt
            FROM MailJobs 
            WHERE Status = 'PendingApproval'
            ORDER BY CreatedAt ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<MailJob>(sql);
    }

    /// <summary>
    /// Updates a job's status.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int jobId, JobStatus newStatus)
    {
        const string sql = @"
            UPDATE MailJobs 
            SET Status = @Status::job_status, UpdatedAt = CURRENT_TIMESTAMP
            WHERE JobId = @JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { JobId = jobId, Status = newStatus.ToString() });
        return affected > 0;
    }

    /// <summary>
    /// Approves or rejects a job using the sp_approve_mail_job stored procedure.
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="approverId">ID of the user approving/rejecting</param>
    /// <param name="approved">True to approve, false to reject</param>
    /// <param name="reason">Rejection reason (if rejecting)</param>
    /// <returns>Result message from the stored procedure</returns>
    public async Task<string> ApproveJobAsync(int jobId, int approverId, bool approved, string? reason = null)
    {
        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "CALL sp_approve_mail_job(@p_job_id, @p_approver_id, @p_approved, @p_result, @p_reason)", 
            connection);

        cmd.Parameters.AddWithValue("p_job_id", jobId);
        cmd.Parameters.AddWithValue("p_approver_id", approverId);
        cmd.Parameters.AddWithValue("p_approved", approved);
        cmd.Parameters.Add(new NpgsqlParameter("p_result", NpgsqlDbType.Text) { Direction = ParameterDirection.Output });
        cmd.Parameters.AddWithValue("p_reason", reason ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();

        return cmd.Parameters["p_result"].Value?.ToString() ?? "Unknown result";
    }



    /// <summary>
    /// Marks a job as started.
    /// </summary>
    public async Task StartJobAsync(int jobId)
    {
        const string sql = @"
            UPDATE MailJobs 
            SET Status = 'Processing'::job_status, 
                StartedAt = CURRENT_TIMESTAMP,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE JobId = @JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { JobId = jobId });
    }

    /// <summary>
    /// Marks a job as completed.
    /// </summary>
    public async Task CompleteJobAsync(int jobId, bool success = true)
    {
        var status = success ? "Completed" : "Failed";
        const string sql = @"
            UPDATE MailJobs 
            SET Status = @Status::job_status, 
                CompletedAt = CURRENT_TIMESTAMP,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE JobId = @JobId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { JobId = jobId, Status = status });
    }

    #endregion

    #region MailJobLog Operations

    /// <summary>
    /// Inserts a log entry for an email send attempt.
    /// </summary>
    /// <param name="log">Log entry data</param>
    /// <returns>The created log ID</returns>
    public async Task<long> InsertLogAsync(MailJobLog log)
    {
        // Serialize LogDetails to JSONB
        var logDetailsJson = log.LogDetails != null 
            ? JsonConvert.SerializeObject(log.LogDetails) 
            : "{}";

        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO MailJobLogs (JobId, ContactId, Status, LogDetails, SentAt, ErrorMessage)
            VALUES (@JobId, @ContactId, @Status::log_status, @LogDetails, @SentAt, @ErrorMessage)
            RETURNING LogId", connection);

        cmd.Parameters.AddWithValue("JobId", log.JobId);
        cmd.Parameters.AddWithValue("ContactId", log.ContactId);
        cmd.Parameters.AddWithValue("Status", log.Status.ToString());
        cmd.Parameters.Add(new NpgsqlParameter("LogDetails", NpgsqlDbType.Jsonb) { Value = logDetailsJson });
        cmd.Parameters.AddWithValue("SentAt", log.SentAt ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("ErrorMessage", log.ErrorMessage ?? (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Bulk inserts multiple log entries.
    /// </summary>
    public async Task BulkInsertLogsAsync(IEnumerable<MailJobLog> logs)
    {
        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        await using var batch = new NpgsqlBatch(connection);

        foreach (var log in logs)
        {
            var logDetailsJson = log.LogDetails != null 
                ? JsonConvert.SerializeObject(log.LogDetails) 
                : "{}";

            var cmd = new NpgsqlBatchCommand(@"
                INSERT INTO MailJobLogs (JobId, ContactId, Status, LogDetails, SentAt, ErrorMessage)
                VALUES ($1, $2, $3::log_status, $4, $5, $6)");

            cmd.Parameters.AddWithValue(log.JobId);
            cmd.Parameters.AddWithValue(log.ContactId);
            cmd.Parameters.AddWithValue(log.Status.ToString());
            cmd.Parameters.Add(new NpgsqlParameter { Value = logDetailsJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
            cmd.Parameters.AddWithValue(log.SentAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(log.ErrorMessage ?? (object)DBNull.Value);

            batch.BatchCommands.Add(cmd);
        }

        await batch.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets all logs for a job.
    /// </summary>
    public async Task<IEnumerable<MailJobLog>> GetLogsByJobIdAsync(int jobId)
    {
        const string sql = @"
            SELECT LogId, JobId, ContactId, Status, LogDetails, SentAt, ErrorMessage, CreatedAt
            FROM MailJobLogs 
            WHERE JobId = @JobId
            ORDER BY CreatedAt DESC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<MailJobLog>(sql, new { JobId = jobId });
    }

    /// <summary>
    /// Gets log statistics for a job.
    /// </summary>
    public async Task<Dictionary<LogStatus, int>> GetLogStatsByJobIdAsync(int jobId)
    {
        const string sql = @"
            SELECT Status, COUNT(*) as Count
            FROM MailJobLogs 
            WHERE JobId = @JobId
            GROUP BY Status";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var results = await connection.QueryAsync<(string Status, int Count)>(sql, new { JobId = jobId });

        var stats = new Dictionary<LogStatus, int>();
        foreach (var (status, count) in results)
        {
            if (Enum.TryParse<LogStatus>(status, true, out var statusEnum))
            {
                stats[statusEnum] = count;
            }
        }
        return stats;
    }

    #endregion
}
