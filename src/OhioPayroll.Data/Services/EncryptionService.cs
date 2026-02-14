using System.Security.Cryptography;
using System.Text;
using OhioPayroll.Core.Interfaces;

namespace OhioPayroll.Data.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public EncryptionService(byte[] key)
    {
        if (key == null || key.Length != 32)
            throw new ArgumentException("Encryption key must be 32 bytes.", nameof(key));
        _key = (byte[])key.Clone();
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        var fullCipher = Convert.FromBase64String(cipherText);
        if (fullCipher.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherBytes = new byte[fullCipher.Length - NonceSize - TagSize];

        Buffer.BlockCopy(fullCipher, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(fullCipher, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(fullCipher, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = new byte[cipherBytes.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
