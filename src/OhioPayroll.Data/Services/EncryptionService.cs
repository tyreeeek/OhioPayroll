using System.Security.Cryptography;
using System.Text;
using OhioPayroll.Core.Interfaces;

namespace OhioPayroll.Data.Services;

public class EncryptionService : IEncryptionService, IDisposable
{
    private readonly byte[] _key;
    private readonly string _keyVersion;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int RequiredKeySize = 32; // 256 bits
    private const string CurrentKeyVersion = "v1";
    private bool _disposed;

    public EncryptionService(byte[] key)
    {
        // FAIL FAST: Validate key at startup
        if (key == null)
            throw new ArgumentNullException(nameof(key), "FATAL: Encryption key cannot be null");

        if (key.Length != RequiredKeySize)
            throw new ArgumentException(
                $"FATAL: Encryption key must be exactly {RequiredKeySize} bytes (256 bits), got {key.Length} bytes. " +
                "This indicates a configuration error that must be fixed before the application can start.",
                nameof(key));

        _key = (byte[])key.Clone();
        _keyVersion = CurrentKeyVersion;

        // Log successful initialization (without exposing key material)
        Console.WriteLine($"[EncryptionService] Initialized with key version {_keyVersion}, key size {key.Length * 8} bits");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        try
        {
            // Prepend key version for rotation support
            var versionBytes = Encoding.UTF8.GetBytes(_keyVersion + "|");
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plainBytes, ciphertext, tag);

            // Format: [version|][nonce][tag][ciphertext]
            var result = new byte[versionBytes.Length + NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(versionBytes, 0, result, 0, versionBytes.Length);
            Buffer.BlockCopy(nonce, 0, result, versionBytes.Length, NonceSize);
            Buffer.BlockCopy(tag, 0, result, versionBytes.Length + NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, versionBytes.Length + NonceSize + TagSize, ciphertext.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Encryption failed: {ex.Message}", ex);
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            // Extract and validate key version
            int versionEndIndex = Array.IndexOf(fullCipher, (byte)'|');
            string storedVersion = "v0"; // Legacy data without version
            int dataOffset = 0;

            if (versionEndIndex > 0 && versionEndIndex < 10) // Reasonable version string length
            {
                storedVersion = Encoding.UTF8.GetString(fullCipher, 0, versionEndIndex);
                dataOffset = versionEndIndex + 1; // Skip past "v1|"
            }

            // Check for key rotation mismatch
            if (storedVersion != _keyVersion && storedVersion != "v0")
            {
                throw new CryptographicException(
                    $"Key rotation mismatch: Data encrypted with {storedVersion}, current key is {_keyVersion}. " +
                    "Database migration required before decryption can proceed.");
            }

            var remainingLength = fullCipher.Length - dataOffset;
            if (remainingLength < NonceSize + TagSize)
                throw new CryptographicException(
                    $"Corrupted encrypted data: Expected at least {NonceSize + TagSize} bytes, got {remainingLength} bytes. " +
                    "This data may be corrupted or tampered with.");

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var cipherBytes = new byte[remainingLength - NonceSize - TagSize];

            Buffer.BlockCopy(fullCipher, dataOffset, nonce, 0, NonceSize);
            Buffer.BlockCopy(fullCipher, dataOffset + NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(fullCipher, dataOffset + NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException(
                "Corrupted encrypted data: Invalid Base64 encoding. The data may be corrupted.", ex);
        }
        catch (CryptographicException)
        {
            // Re-throw crypto exceptions with original message
            throw;
        }
        catch (Exception ex)
        {
            throw new CryptographicException(
                $"Decryption failed: {ex.Message}. The data may be corrupted or encrypted with a different key.", ex);
        }
    }
}
