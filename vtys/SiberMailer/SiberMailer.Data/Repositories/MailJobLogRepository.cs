using Dapper;
using System.Text.Json;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for MailJobLogs database operations for the Log Viewer.
/// </summary>
public class MailJobLogRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public MailJobLogRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets all logs with pagination.
    /// </summary>
    public async Task<IEnumerable<LogViewEntry>> GetLogsAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT l.LogId, l.JobId, l.ContactId, l.Status as LogType, 
                   l.LogDetails::text as LogDetailsJson, l.SentAt, l.ErrorMessage as LogMessage, 
                   l.CreatedAt as LogTimestamp,
                   j.JobName as JobSubject, c.Email as ContactEmail
            FROM MailJobLogs l
            LEFT JOIN MailJobs j ON l.JobId = j.JobId
            LEFT JOIN Contacts c ON l.ContactId = c.ContactId
            ORDER BY l.CreatedAt DESC
            LIMIT @Limit OFFSET @Offset";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<LogViewEntry>(sql, new { Limit = limit, Offset = offset });
    }

    /// <summary>
    /// Gets logs for a specific job.
    /// </summary>
    public async Task<IEnumerable<LogViewEntry>> GetLogsByJobIdAsync(int jobId)
    {
        const string sql = @"
            SELECT l.LogId, l.JobId, l.ContactId, l.Status as LogType, 
                   l.LogDetails::text as LogDetailsJson, l.SentAt, l.ErrorMessage as LogMessage, 
                   l.CreatedAt as LogTimestamp,
                   j.JobName as JobSubject, c.Email as ContactEmail
            FROM MailJobLogs l
            LEFT JOIN MailJobs j ON l.JobId = j.JobId
            LEFT JOIN Contacts c ON l.ContactId = c.ContactId
            WHERE l.JobId = @JobId
            ORDER BY l.CreatedAt DESC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<LogViewEntry>(sql, new { JobId = jobId });
    }

    /// <summary>
    /// Gets logs by log type/status.
    /// </summary>
    public async Task<IEnumerable<LogViewEntry>> GetLogsByTypeAsync(string logType, int limit = 100)
    {
        const string sql = @"
            SELECT l.LogId, l.JobId, l.ContactId, l.Status as LogType, 
                   l.LogDetails::text as LogDetailsJson, l.SentAt, l.ErrorMessage as LogMessage, 
                   l.CreatedAt as LogTimestamp,
                   j.JobName as JobSubject, c.Email as ContactEmail
            FROM MailJobLogs l
            LEFT JOIN MailJobs j ON l.JobId = j.JobId
            LEFT JOIN Contacts c ON l.ContactId = c.ContactId
            WHERE l.Status::text = @LogType
            ORDER BY l.CreatedAt DESC
            LIMIT @Limit";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<LogViewEntry>(sql, new { LogType = logType, Limit = limit });
    }

    /// <summary>
    /// Search logs by message content.
    /// </summary>
    public async Task<IEnumerable<LogViewEntry>> SearchLogsAsync(string searchTerm, int limit = 100)
    {
        const string sql = @"
            SELECT l.LogId, l.JobId, l.ContactId, l.Status as LogType, 
                   l.LogDetails::text as LogDetailsJson, l.SentAt, l.ErrorMessage as LogMessage, 
                   l.CreatedAt as LogTimestamp,
                   j.JobName as JobSubject, c.Email as ContactEmail
            FROM MailJobLogs l
            LEFT JOIN MailJobs j ON l.JobId = j.JobId
            LEFT JOIN Contacts c ON l.ContactId = c.ContactId
            WHERE l.ErrorMessage ILIKE @SearchTerm 
               OR j.JobName ILIKE @SearchTerm
               OR c.Email ILIKE @SearchTerm
            ORDER BY l.CreatedAt DESC
            LIMIT @Limit";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<LogViewEntry>(sql, new { SearchTerm = $"%{searchTerm}%", Limit = limit });
    }

    /// <summary>
    /// Gets distinct log types for filtering.
    /// </summary>
    public async Task<IEnumerable<string>> GetLogTypesAsync()
    {
        const string sql = "SELECT DISTINCT Status::text FROM MailJobLogs ORDER BY Status::text";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<string>(sql);
    }
}

/// <summary>
/// Log entry for the Log Viewer UI.
/// </summary>
public class LogViewEntry
{
    public long LogId { get; set; }
    public int? JobId { get; set; }
    public int? ContactId { get; set; }
    public string LogType { get; set; } = string.Empty;
    public string? LogMessage { get; set; }
    public string? LogDetailsJson { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime LogTimestamp { get; set; }

    // Joined fields
    public string? JobSubject { get; set; }
    public string? ContactEmail { get; set; }

    // Computed properties
    public string LogTypeIcon => LogType?.ToUpper() switch
    {
        "SENT" => "✅",
        "FAILED" => "❌",
        "BOUNCED" => "↩️",
        "PENDING" => "⏳",
        "QUEUED" => "📥",
        _ => "📝"
    };

    /// <summary>
    /// Parses JSONB LogDetails into a formatted string.
    /// </summary>
    public string FormattedDetails
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogDetailsJson))
                return string.Empty;

            try
            {
                var jsonDoc = JsonDocument.Parse(LogDetailsJson);
                return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch
            {
                return LogDetailsJson;
            }
        }
    }

    /// <summary>
    /// Gets key-value pairs from JSONB for display.
    /// </summary>
    public Dictionary<string, string> DetailsDictionary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogDetailsJson))
                return new Dictionary<string, string>();

            try
            {
                var result = new Dictionary<string, string>();
                var jsonDoc = JsonDocument.Parse(LogDetailsJson);
                
                foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ToString();
                }
                
                return result;
            }
            catch
            {
                return new Dictionary<string, string> { ["raw"] = LogDetailsJson };
            }
        }
    }
}
