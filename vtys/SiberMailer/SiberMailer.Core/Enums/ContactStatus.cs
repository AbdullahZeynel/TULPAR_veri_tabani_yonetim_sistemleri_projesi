namespace SiberMailer.Core.Enums;

/// <summary>
/// Contact status enum matching PostgreSQL 'contact_status' type.
/// </summary>
public enum ContactStatus
{
    Active,
    Unsubscribed,
    Bounced,
    RedListed,
    Pending
}
