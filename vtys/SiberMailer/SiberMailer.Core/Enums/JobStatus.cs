namespace SiberMailer.Core.Enums;

/// <summary>
/// Mail job status enum matching PostgreSQL 'job_status' type.
/// </summary>
public enum JobStatus
{
    Draft,
    PendingApproval,
    Approved,
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Mail job log status enum matching PostgreSQL 'log_status' type.
/// </summary>
public enum LogStatus
{
    Sent,
    Failed,
    Bounced,
    Opened,
    Clicked
}
