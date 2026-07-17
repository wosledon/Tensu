using System.Security.Cryptography;
using System.Text;

namespace Tensu.Api.Infrastructure;

public class EncryptionService
{
    /// <summary>Prefix marking encrypted audit content (versioned for future key rotation).</summary>
    public const string AuditContentPrefix = "enc:v1:";

    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Encryption:Key"] ?? "TensuDefaultEncryptionKey32Bytes!";
        _key = Encoding.UTF8.GetBytes(keyString.PadRight(32)[..32]);
    }

    /// <summary>
    /// Encrypts audit content with the versioned prefix, unless already encrypted.
    /// </summary>
    public string? EncryptAuditContent(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        if (plainText.StartsWith(AuditContentPrefix, StringComparison.Ordinal)) return plainText;
        return AuditContentPrefix + Encrypt(plainText);
    }

    /// <summary>
    /// Decrypts audit content if it carries the versioned prefix; otherwise returns it unchanged.
    /// Returns null on decryption failure of prefixed content (wrong key / corrupted data).
    /// </summary>
    public string? DecryptAuditContent(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        if (!content.StartsWith(AuditContentPrefix, StringComparison.Ordinal)) return content;
        try
        {
            return Decrypt(content[AuditContentPrefix.Length..]);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(plainText);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullBuffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[16];
        Array.Copy(fullBuffer, 0, iv, 0, 16);
        aes.IV = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(fullBuffer, 16, fullBuffer.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}
