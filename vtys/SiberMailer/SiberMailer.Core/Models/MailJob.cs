using SiberMailer.Core.Enums;

namespace SiberMailer.Core.Models;

/// <summary>
/// MailJob entity matching the PostgreSQL 'MailJobs' table.
/// Represents an email campaign/send job.
/// </summary>
public class MailJob
{
    /// <summary>Primary key</summary>
    public int JobId { get; set; }

    /// <summary>Job/Campaign name</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>Foreign key to Branches</summary>
    public int BranchId { get; set; }

    /// <summary>Foreign key to RecipientLists</summary>
    public int ListId { get; set; }

    /// <summary>Foreign key to Templates</summary>
    public int TemplateId { get; set; }

    /// <summary>Foreign key to SmtpAccounts</summary>
    public int SmtpAccountId { get; set; }

    /// <summary>Current job status</summary>
    public JobStatus Status { get; set; } = JobStatus.Draft;

    /// <summary>Total number of recipients</summary>
    public int TotalRecipients { get; set; }



    /// <summary>When processing started</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When processing completed</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Whether manager approval is required</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>User ID who approved the job</summary>
    public int? ApprovedBy { get; set; }

    /// <summary>When the job was approved</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Reason for rejection (if rejected)</summary>
    public string? RejectionReason { get; set; }

    /// <summary>User ID who created the job</summary>
    public int CreatedBy { get; set; }

    /// <summary>Record creation timestamp</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Custom email subject (overrides template default)</summary>
    public string? EmailSubject { get; set; }

    /// <summary>Admin notes/description for approval review</summary>
    public string? AdminNotes { get; set; }

    /// <summary>JSON array of attachment file paths</summary>
    public List<string>? AttachmentPaths { get; set; }

    // Computed properties

    /// <summary>Progress percentage (0-100) - calculated from logs</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Whether the job is in a final state</summary>
    public bool IsCompleted => Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;

    /// <summary>Whether the job can be edited</summary>
    public bool IsEditable => Status == JobStatus.Draft;

    /// <summary>Whether the job is waiting for approval</summary>
    public bool IsPendingApproval => Status == JobStatus.PendingApproval;

    public override string ToString() => $"{JobName} ({Status})";
}

/// <summary>
/// MailJobLog entity matching the PostgreSQL 'MailJobLogs' table.
/// Tracks individual email send attempts.
/// </summary>
public class MailJobLog
{
    /// <summary>Primary key</summary>
    public long LogId { get; set; }

    /// <summary>Foreign key to MailJobs</summary>
    public int JobId { get; set; }

    /// <summary>Foreign key to Contacts</summary>
    public int ContactId { get; set; }

    /// <summary>Log entry status</summary>
    public LogStatus Status { get; set; } = LogStatus.Sent;

    /// <summary>JSONB log details (smtp_response, delivery_time_ms, etc.)</summary>
    public Dictionary<string, object>? LogDetails { get; set; }

    /// <summary>When the email was sent</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Record creation timestamp</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new mail job.
/// </summary>
public class CreateMailJobDto
{
    public string JobName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public int ListId { get; set; }
    public int TemplateId { get; set; }
    public int SmtpAccountId { get; set; }
    public int CreatedBy { get; set; }
    public bool RequiresApproval { get; set; }

}
