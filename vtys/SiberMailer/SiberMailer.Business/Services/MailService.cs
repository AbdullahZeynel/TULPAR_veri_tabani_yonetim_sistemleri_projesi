using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using System.Text.RegularExpressions;

namespace SiberMailer.Business.Services;

/// <summary>
/// Service for sending emails using MailKit.
/// Handles template parsing and batch sending.
/// </summary>
public class MailService
{
    // Default timeout for SMTP operations
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Result of a single email send attempt.
    /// </summary>
    public class SendResult
    {
        public int ContactId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int DeliveryTimeMs { get; set; }
        public DateTime SentAt { get; set; }

        public MailJobLog ToLog(int jobId) => new()
        {
            JobId = jobId,
            ContactId = ContactId,
            Status = Success ? LogStatus.Sent : LogStatus.Failed,
            SentAt = Success ? SentAt : null,
            ErrorMessage = ErrorMessage,
            LogDetails = new Dictionary<string, object>
            {
                ["delivery_time_ms"] = DeliveryTimeMs,
                ["email"] = Email,
                ["timestamp"] = SentAt.ToString("O")
            }
        };
    }

    /// <summary>
    /// Result of a batch send operation.
    /// </summary>
    public class BatchSendResult
    {
        public int TotalAttempted { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<SendResult> Results { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }

        public double SuccessRate => TotalAttempted > 0 
            ? (double)SuccessCount / TotalAttempted * 100 
            : 0;
    }

    /// <summary>
    /// Sends a batch of emails using the provided SMTP configuration.
    /// </summary>
    /// <param name="smtpConfig">Decrypted SMTP configuration</param>
    /// <param name="contacts">List of contacts to send to</param>
    /// <param name="subject">Email subject (supports merge tags)</param>
    /// <param name="htmlBody">HTML body template (supports merge tags)</param>
    /// <param name="plainTextBody">Optional plain text body</param>
    /// <param name="progress">Optional progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch send result with individual outcomes</returns>
    public async Task<BatchSendResult> SendBatchAsync(
        DecryptedSmtpConfig smtpConfig,
        IEnumerable<Contact> contacts,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        IProgress<(int Sent, int Total, string CurrentEmail)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var contactList = contacts.ToList();
        var result = new BatchSendResult { TotalAttempted = contactList.Count };
        var startTime = DateTime.UtcNow;

        using var client = new SmtpClient();
        client.Timeout = (int)DefaultTimeout.TotalMilliseconds;

        try
        {
            // Connect to SMTP server
            var secureOption = smtpConfig.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(
                smtpConfig.SmtpHost, 
                smtpConfig.SmtpPort, 
                secureOption, 
                cancellationToken);

            // Authenticate
            await client.AuthenticateAsync(
                smtpConfig.Email, 
                smtpConfig.Password, 
                cancellationToken);

            // Send to each contact
            int sentCount = 0;
            foreach (var contact in contactList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var sendResult = await SendSingleAsync(
                    client, 
                    smtpConfig.Email, 
                    contact, 
                    subject, 
                    htmlBody,
                    plainTextBody);

                result.Results.Add(sendResult);

                if (sendResult.Success)
                    result.SuccessCount++;
                else
                    result.FailedCount++;

                sentCount++;
                progress?.Report((sentCount, contactList.Count, contact.Email));

                // Small delay to avoid rate limiting
                await Task.Delay(3000, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Connection-level failure - mark remaining as failed
            foreach (var contact in contactList.Skip(result.Results.Count))
            {
                result.Results.Add(new SendResult
                {
                    ContactId = contact.ContactId,
                    Email = contact.Email,
                    Success = false,
                    ErrorMessage = $"Connection error: {ex.Message}",
                    SentAt = DateTime.UtcNow
                });
                result.FailedCount++;
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }

        result.TotalDuration = DateTime.UtcNow - startTime;
        return result;
    }

    /// <summary>
    /// Sends a single email to one contact.
    /// </summary>
    private async Task<SendResult> SendSingleAsync(
        SmtpClient client,
        string fromEmail,
        Contact contact,
        string subjectTemplate,
        string htmlBodyTemplate,
        string? plainTextTemplate)
    {
        var sendStart = DateTime.UtcNow;
        var result = new SendResult
        {
            ContactId = contact.ContactId,
            Email = contact.Email,
            SentAt = sendStart
        };


        const int MaxRetries = 3;
        int attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                // Parse templates with contact data
                var parsedSubject = ParseTemplate(subjectTemplate, contact);
                var parsedHtml = ParseTemplate(htmlBodyTemplate, contact);
                var parsedPlainText = plainTextTemplate != null 
                    ? ParseTemplate(plainTextTemplate, contact) 
                    : null;

                // Build the message
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(fromEmail));
                message.To.Add(MailboxAddress.Parse(contact.Email));
                message.Subject = parsedSubject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = parsedHtml
                };

                if (!string.IsNullOrEmpty(parsedPlainText))
                {
                    bodyBuilder.TextBody = parsedPlainText;
                }

                message.Body = bodyBuilder.ToMessageBody();

                // Send
                await client.SendAsync(message);

                result.Success = true;
                result.DeliveryTimeMs = (int)(DateTime.UtcNow - sendStart).TotalMilliseconds;
                return result; // Success - return immediately
            }
            catch (Exception ex)
            {
                if (attempt >= MaxRetries)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.DeliveryTimeMs = (int)(DateTime.UtcNow - sendStart).TotalMilliseconds;
                    return result;
                }
                
                // Wait before retrying (exponential backoff: 1s, 2s, 4s...)
                await Task.Delay(1000 * (int)Math.Pow(2, attempt - 1));
            }
        }
    }

    /// <summary>
    /// Parses a template string, replacing merge tags with contact data.
    /// Supports: {FullName}, {Email}, {Company}, and custom JSONB fields.
    /// </summary>
    /// <param name="template">Template with merge tags</param>
    /// <param name="contact">Contact with data to merge</param>
    /// <returns>Parsed string with replaced values</returns>
    public string ParseTemplate(string template, Contact contact)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;

        // Standard placeholders
        result = ReplacePlaceholder(result, "FullName", contact.FullName ?? "");
        result = ReplacePlaceholder(result, "Name", contact.FullName ?? "");
        result = ReplacePlaceholder(result, "Email", contact.Email);
        result = ReplacePlaceholder(result, "Company", contact.Company ?? "");

        // First name (extract from FullName)
        var firstName = contact.FullName?.Split(' ').FirstOrDefault() ?? "";
        result = ReplacePlaceholder(result, "FirstName", firstName);

        // Custom JSONB data
        if (!string.IsNullOrEmpty(contact.CustomData))
        {
            try
            {
                var customDataDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(contact.CustomData);
                if (customDataDict != null)
                {
                    foreach (var kvp in customDataDict)
                    {
                        result = ReplacePlaceholder(result, kvp.Key, kvp.Value?.ToString() ?? "");
                    }
                }
            }
            catch
            {
                // Invalid JSON - skip custom data substitution
            }
        }

        return result;
    }

    /// <summary>
    /// Replaces a placeholder in the template (case-insensitive).
    /// </summary>
    private string ReplacePlaceholder(string template, string placeholder, string value)
    {
        // Match {placeholder} with optional whitespace
        var pattern = @"\{\s*" + Regex.Escape(placeholder) + @"\s*\}";
        return Regex.Replace(template, pattern, value, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Sends a single test email.
    /// </summary>
    public async Task<SendResult> SendTestEmailAsync(
        DecryptedSmtpConfig smtpConfig,
        string toEmail,
        string subject,
        string htmlBody)
    {
        var testContact = new Contact
        {
            ContactId = 0,
            Email = toEmail,
            FullName = "Test User",
            Company = "Test Company"
        };

        using var client = new SmtpClient();
        client.Timeout = (int)DefaultTimeout.TotalMilliseconds;

        try
        {
            var secureOption = smtpConfig.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(smtpConfig.SmtpHost, smtpConfig.SmtpPort, secureOption);
            await client.AuthenticateAsync(smtpConfig.Email, smtpConfig.Password);

            var result = await SendSingleAsync(client, smtpConfig.Email, testContact, subject, htmlBody, null);

            await client.DisconnectAsync(true);

            return result;
        }
        catch (Exception ex)
        {
            return new SendResult
            {
                ContactId = 0,
                Email = toEmail,
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Validates SMTP connection without sending an email.
    /// </summary>
    public async Task<(bool Success, string Message)> ValidateConnectionAsync(DecryptedSmtpConfig smtpConfig)
    {
        using var client = new SmtpClient();
        client.Timeout = (int)DefaultTimeout.TotalMilliseconds;

        try
        {
            var secureOption = smtpConfig.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(smtpConfig.SmtpHost, smtpConfig.SmtpPort, secureOption);
            await client.AuthenticateAsync(smtpConfig.Email, smtpConfig.Password);
            await client.DisconnectAsync(true);

            return (true, "Connection successful");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
