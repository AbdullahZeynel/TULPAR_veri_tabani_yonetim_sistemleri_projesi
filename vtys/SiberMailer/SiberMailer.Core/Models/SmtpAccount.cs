namespace SiberMailer.Core.Models;

/// <summary>
/// SmtpAccount entity matching the PostgreSQL 'SmtpAccounts' table.
/// This is the "Vault" - stores encrypted SMTP credentials.
/// </summary>
public class SmtpAccount
{
    /// <summary>Primary key</summary>
    public int SmtpAccountId { get; set; }

    /// <summary>Display name for the account</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>SMTP server hostname</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP server port (usually 587 or 465)</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Whether to use SSL/TLS</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>SMTP email address (plain text, not encrypted)</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted password (Base64)</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>Initialization Vector for AES decryption (Base64)</summary>
    public string EncryptionIV { get; set; } = string.Empty;

    /// <summary>Maximum emails per day (rate limiting)</summary>
    public int DailyLimit { get; set; } = 500;

    /// <summary>Emails sent today</summary>
    public int SentToday { get; set; } = 0;

    /// <summary>Last date the counter was reset</summary>
    public DateTime? LastResetDate { get; set; }

    /// <summary>Branch that owns this account</summary>
    public int OwnerBranchId { get; set; }

    /// <summary>Whether other users can use this account</summary>
    public bool IsShared { get; set; } = false;

    /// <summary>Whether the account is active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Record creation timestamp</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp</summary>
    public DateTime UpdatedAt { get; set; }

    // Audit Trail
    
    /// <summary>User who created this account</summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>User who last updated this account</summary>
    public int? UpdatedByUserId { get; set; }

    /// <summary>Name of user who created this account</summary>
    public string CreatedByName { get; set; } = "System";

    /// <summary>Name of user who last updated this account</summary>
    public string UpdatedByName { get; set; } = "System";

    // Statistics (from vw_SmtpAccountStats view)
    
    /// <summary>Total emails sent all time</summary>
    public int TotalSentCount { get; set; }

    /// <summary>Emails sent today</summary>
    public int TodaySentCount { get; set; }

    /// <summary>Last time an email was sent</summary>
    public DateTime? LastSentAt { get; set; }

    // Computed properties

    /// <summary>Remaining emails that can be sent today</summary>
    public int RemainingToday => Math.Max(0, DailyLimit - SentToday);

    /// <summary>Whether daily limit has been reached</summary>
    public bool IsLimitReached => SentToday >= DailyLimit;

    public override string ToString() => $"{AccountName} ({SmtpHost}:{SmtpPort})";
}

/// <summary>
/// DTO for creating a new SMTP account with plain text credentials.
/// The service will encrypt these before storage.
/// </summary>
public class CreateSmtpAccountDto
{
    public string AccountName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    
    /// <summary>Plain text email (will be encrypted)</summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>Plain text password (will be encrypted)</summary>
    public string Password { get; set; } = string.Empty;
    
    /// <summary>PIN to encrypt credentials</summary>
    public string Pin { get; set; } = string.Empty;
    
    public int DailyLimit { get; set; } = 500;
    public int OwnerBranchId { get; set; }
    public bool IsShared { get; set; } = false;
}

/// <summary>
/// Decrypted SMTP configuration for use during mail sending.
/// Never store this - generate on demand and dispose after use.
/// </summary>
public class DecryptedSmtpConfig
{
    public int SmtpAccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    
    /// <summary>Decrypted email address</summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>Decrypted password</summary>
    public string Password { get; set; } = string.Empty;
    
    public int RemainingToday { get; set; }
}
