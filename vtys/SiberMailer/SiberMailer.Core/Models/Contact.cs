using Newtonsoft.Json;
using SiberMailer.Core.Enums;

namespace SiberMailer.Core.Models;

/// <summary>
/// Contact entity matching the PostgreSQL 'Contacts' table.
/// </summary>
public class Contact
{
    /// <summary>Primary key</summary>
    public int ContactId { get; set; }

    /// <summary>Foreign key to RecipientLists</summary>
    public int ListId { get; set; }

    /// <summary>Email address (unique within list)</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Full name of the contact</summary>
    public string? FullName { get; set; }

    /// <summary>Company name</summary>
    public string? Company { get; set; }

    /// <summary>Flexible JSONB custom data stored as JSON string (e.g., "{\"Year\": 2024, \"Department\": \"IT\"}")</summary>
    public string? CustomData { get; set; }

    /// <summary>Contact status: Active, Unsubscribed, Bounced, RedListed, Pending</summary>
    public ContactStatus Status { get; set; } = ContactStatus.Active;

    /// <summary>Number of times email bounced</summary>
    public int BounceCount { get; set; } = 0;

    /// <summary>Last bounce timestamp</summary>
    public DateTime? LastBouncedAt { get; set; }

    /// <summary>Unsubscribe timestamp</summary>
    public DateTime? UnsubscribedAt { get; set; }

    /// <summary>Record creation timestamp</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Returns a display-friendly string representation.
    /// </summary>
    public override string ToString() => 
        string.IsNullOrEmpty(FullName) ? Email : $"{FullName} <{Email}>";
}

/// <summary>
/// DTO for bulk import operations.
/// Matches the expected JSONB structure for sp_import_contacts_bulk.
/// </summary>
public class ContactImportDto
{
    [JsonProperty("Email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("FullName")]
    public string? FullName { get; set; }

    [JsonProperty("Company")]
    public string? Company { get; set; }

    [JsonProperty("CustomData")]
    public Dictionary<string, object>? CustomData { get; set; }
}

/// <summary>
/// Result of a bulk import operation.
/// </summary>
public class BulkImportResult
{
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Total => Inserted + Updated + Skipped;

    public override string ToString() => 
        $"Import Complete: {Inserted} inserted, {Updated} updated, {Skipped} skipped";
}
