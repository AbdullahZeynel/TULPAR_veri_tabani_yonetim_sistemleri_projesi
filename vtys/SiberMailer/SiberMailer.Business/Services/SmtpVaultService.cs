using SiberMailer.Core.Models;
using SiberMailer.Data.Repositories;

namespace SiberMailer.Business.Services;

/// <summary>
/// Service for managing encrypted SMTP accounts (The Vault).
/// Handles encryption/decryption of credentials using CryptoService.
/// </summary>
public class SmtpVaultService
{
    private readonly SmtpAccountRepository _repository;
    private readonly CryptoService _crypto;

    public SmtpVaultService(SmtpAccountRepository repository, CryptoService? crypto = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _crypto = crypto ?? new CryptoService();
    }

    /// <summary>
    /// Creates a new SMTP account with encrypted credentials.
    /// </summary>
    /// <param name="dto">Account creation data with plain text credentials</param>
    /// <returns>The created account ID</returns>
    public async Task<int> CreateAccountAsync(CreateSmtpAccountDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Pin))
            throw new ArgumentException("PIN is required for encryption", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Password is required", nameof(dto));

        // Encrypt only the password (Email is now stored as plain text)
        var encryptedPassword = _crypto.EncryptWithSeparateIv(dto.Password, dto.Pin, out var iv);

        var account = new SmtpAccount
        {
            AccountName = dto.AccountName,
            SmtpHost = dto.SmtpHost,
            SmtpPort = dto.SmtpPort,
            UseSsl = dto.UseSsl,
            Email = dto.Email, // Plain text - not encrypted
            EncryptedPassword = encryptedPassword,
            EncryptionIV = iv,
            DailyLimit = dto.DailyLimit,
            OwnerBranchId = dto.OwnerBranchId,
            IsShared = dto.IsShared
        };

        return await _repository.CreateAsync(account);
    }

    /// <summary>
    /// Decrypts and returns SMTP configuration for use.
    /// Call this only when actively sending emails.
    /// </summary>
    /// <param name="accountId">The SMTP account ID</param>
    /// <param name="pin">The user's PIN to decrypt credentials</param>
    /// <returns>Decrypted config or null if not found</returns>
    /// <exception cref="CryptographicException">If PIN is incorrect</exception>
    public async Task<DecryptedSmtpConfig?> GetDecryptedConfigAsync(int accountId, string pin)
    {
        var account = await _repository.GetByIdAsync(accountId);
        if (account == null)
            return null;

        if (!account.IsActive)
            throw new InvalidOperationException("SMTP account is inactive");

        if (account.IsLimitReached)
            throw new InvalidOperationException($"Daily limit reached ({account.DailyLimit} emails)");

        try
        {
            // Email is now plain text - no decryption needed
            var email = account.Email;
            // Decrypt only the password
            var password = _crypto.DecryptWithSeparateIv(account.EncryptedPassword, pin, account.EncryptionIV);

            return new DecryptedSmtpConfig
            {
                SmtpAccountId = account.SmtpAccountId,
                AccountName = account.AccountName,
                SmtpHost = account.SmtpHost,
                SmtpPort = account.SmtpPort,
                UseSsl = account.UseSsl,
                Email = email,
                Password = password,
                RemainingToday = account.RemainingToday
            };
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException("Invalid PIN - decryption failed", ex);
        }
    }

    /// <summary>
    /// Validates that a PIN can decrypt the account credentials.
    /// </summary>
    /// <param name="accountId">The SMTP account ID</param>
    /// <param name="pin">The PIN to validate</param>
    /// <returns>True if PIN is correct</returns>
    public async Task<bool> ValidatePinAsync(int accountId, string pin)
    {
        try
        {
            var config = await GetDecryptedConfigAsync(accountId, pin);
            return config != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all SMTP accounts accessible by a user (without decrypted credentials).
    /// </summary>
    public async Task<IEnumerable<SmtpAccount>> GetAccessibleAccountsAsync(int userId)
    {
        return await _repository.GetAccessibleByUserAsync(userId);
    }

    /// <summary>
    /// Gets all active SMTP accounts.
    /// </summary>
    public async Task<IEnumerable<SmtpAccount>> GetAllAccountsAsync()
    {
        return await _repository.GetAllAsync(activeOnly: true);
    }

    /// <summary>
    /// Gets an account by ID (without decrypting credentials).
    /// </summary>
    public async Task<SmtpAccount?> GetAccountAsync(int accountId)
    {
        return await _repository.GetByIdAsync(accountId);
    }

    /// <summary>
    /// Updates account credentials with new encryption.
    /// </summary>
    /// <param name="accountId">Account to update</param>
    /// <param name="newEmail">New email (plain text)</param>
    /// <param name="newPassword">New password (plain text)</param>
    /// <param name="newPin">New PIN for encryption</param>
    public async Task<bool> UpdateCredentialsAsync(int accountId, string newEmail, string newPassword, string newPin)
    {
        if (string.IsNullOrWhiteSpace(newPin))
            throw new ArgumentException("PIN is required", nameof(newPin));

        // Email is now stored as plain text (not encrypted)
        var encryptedPassword = _crypto.EncryptWithSeparateIv(newPassword, newPin, out var iv);

        return await _repository.UpdateCredentialsAsync(accountId, newEmail, encryptedPassword, iv);
    }

    /// <summary>
    /// Records emails sent for rate limiting.
    /// </summary>
    public async Task RecordEmailsSentAsync(int accountId, int count = 1)
    {
        await _repository.IncrementSentCountAsync(accountId, count);
    }

    /// <summary>
    /// Checks if an account can send more emails today.
    /// </summary>
    public async Task<(bool CanSend, int Remaining)> CheckRateLimitAsync(int accountId)
    {
        var account = await _repository.GetByIdAsync(accountId);
        if (account == null)
            return (false, 0);

        return (!account.IsLimitReached, account.RemainingToday);
    }

    /// <summary>
    /// Deactivates an SMTP account.
    /// </summary>
    public async Task<bool> DeactivateAccountAsync(int accountId)
    {
        return await _repository.DeactivateAsync(accountId);
    }

    /// <summary>
    /// Permanently deletes an SMTP account.
    /// </summary>
    public async Task<bool> DeleteAccountAsync(int accountId)
    {
        return await _repository.DeleteAsync(accountId);
    }
}
