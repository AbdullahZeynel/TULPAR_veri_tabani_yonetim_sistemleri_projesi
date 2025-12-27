using System.Security.Cryptography;
using System.Text;

namespace SiberMailer.Business.Services;

/// <summary>
/// Provides AES-256 encryption and decryption services for the SMTP Vault.
/// Uses PBKDF2 (Rfc2898DeriveBytes) for key derivation from a PIN.
/// </summary>
public class CryptoService
{
    // AES-256 requires 256-bit (32 byte) key
    private const int KeySize = 256;
    private const int KeyBytes = KeySize / 8; // 32 bytes
    
    // AES block size is 128 bits (16 bytes)
    private const int BlockSize = 128;
    private const int IvBytes = BlockSize / 8; // 16 bytes
    
    // PBKDF2 iterations (higher = more secure, but slower)
    private const int Iterations = 100000;

    // Default salt for key derivation (can be overridden)
    private static readonly byte[] DefaultSalt = Encoding.UTF8.GetBytes("SiberMailer2.0_SMTP_Vault_Salt!");

    /// <summary>
    /// Encrypts a plain text string using AES-256-CBC with a PIN-derived key.
    /// </summary>
    /// <param name="plainText">The text to encrypt</param>
    /// <param name="pin">The user's PIN/password for encryption</param>
    /// <param name="salt">Optional custom salt (uses default if null)</param>
    /// <returns>Base64-encoded encrypted string with embedded IV</returns>
    public string EncryptString(string plainText, string pin, byte[]? salt = null)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentNullException(nameof(pin));

        salt ??= DefaultSalt;

        // Derive key from PIN using PBKDF2
        using var keyDerivation = new Rfc2898DeriveBytes(
            pin, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256);

        var key = keyDerivation.GetBytes(KeyBytes);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV(); // Generate random IV for each encryption

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data for storage
        // Format: [IV (16 bytes)][Encrypted Data]
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts an AES-256-CBC encrypted string using a PIN-derived key.
    /// </summary>
    /// <param name="cipherText">Base64-encoded encrypted string with embedded IV</param>
    /// <param name="pin">The user's PIN/password for decryption</param>
    /// <param name="salt">Optional custom salt (must match encryption salt)</param>
    /// <returns>Decrypted plain text string</returns>
    public string DecryptString(string cipherText, string pin, byte[]? salt = null)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentNullException(nameof(pin));

        salt ??= DefaultSalt;

        // Derive the same key from PIN using PBKDF2
        using var keyDerivation = new Rfc2898DeriveBytes(
            pin, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256);

        var key = keyDerivation.GetBytes(KeyBytes);

        // Decode from Base64
        var fullCipher = Convert.FromBase64String(cipherText);

        // Extract IV (first 16 bytes) and encrypted data
        var iv = new byte[IvBytes];
        var encryptedBytes = new byte[fullCipher.Length - IvBytes];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, IvBytes);
        Buffer.BlockCopy(fullCipher, IvBytes, encryptedBytes, 0, encryptedBytes.Length);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// Encrypts a string and returns the IV separately (for database storage).
    /// </summary>
    /// <param name="plainText">The text to encrypt</param>
    /// <param name="pin">The user's PIN/password</param>
    /// <param name="iv">Output: The generated IV (Base64)</param>
    /// <param name="salt">Optional custom salt</param>
    /// <returns>Base64-encoded encrypted data (without IV)</returns>
    public string EncryptWithSeparateIv(string plainText, string pin, out string iv, byte[]? salt = null)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentNullException(nameof(pin));

        salt ??= DefaultSalt;

        using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
        var key = keyDerivation.GetBytes(KeyBytes);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        iv = Convert.ToBase64String(aes.IV);

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a string using a separately stored IV.
    /// </summary>
    /// <param name="cipherText">Base64-encoded encrypted data</param>
    /// <param name="pin">The user's PIN/password</param>
    /// <param name="iv">The IV used during encryption (Base64)</param>
    /// <param name="salt">Optional custom salt</param>
    /// <returns>Decrypted plain text</returns>
    public string DecryptWithSeparateIv(string cipherText, string pin, string iv, byte[]? salt = null)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentNullException(nameof(pin));
        if (string.IsNullOrEmpty(iv))
            throw new ArgumentNullException(nameof(iv));

        salt ??= DefaultSalt;

        using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
        var key = keyDerivation.GetBytes(KeyBytes);

        var ivBytes = Convert.FromBase64String(iv);
        var encryptedBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = ivBytes;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// Generates a cryptographically secure random salt.
    /// </summary>
    /// <param name="length">Salt length in bytes (default 32)</param>
    /// <returns>Random salt bytes</returns>
    public static byte[] GenerateSalt(int length = 32)
    {
        var salt = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    /// <summary>
    /// Hashes a PIN using SHA-256 for storage/verification.
    /// </summary>
    /// <param name="pin">The PIN to hash</param>
    /// <returns>Hexadecimal hash string</returns>
    public static string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(pin);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    /// <summary>
    /// Verifies a PIN against a stored hash.
    /// </summary>
    /// <param name="pin">The PIN to verify</param>
    /// <param name="storedHash">The stored hash to compare against</param>
    /// <returns>True if PIN matches</returns>
    public static bool VerifyPin(string pin, string storedHash)
    {
        var pinHash = HashPin(pin);
        return string.Equals(pinHash, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
