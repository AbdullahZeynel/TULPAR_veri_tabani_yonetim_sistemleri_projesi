using System.ComponentModel.DataAnnotations.Schema;

namespace SiberMailer.Core.Models;

/// <summary>
/// Template entity matching the PostgreSQL 'Templates' table.
/// Represents an email HTML template that can be used for campaigns.
/// </summary>
public class Template
{
    /// <summary>Primary key.</summary>
    public int TemplateId { get; set; }

    /// <summary>Display name for the template.</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>The HTML content of the email template.</summary>
    [Column("htmlbody")]
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>Foreign key to the Branches table.</summary>
    public int BranchId { get; set; }

    /// <summary>Whether the template is active and available for use.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp when the template was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the template was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Navigation property - Branch name (populated by JOIN).</summary>
    public string? BranchName { get; set; }
}
