using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Otrade.Application.Services.Security;

public class EncryptionService
{
    private readonly string _key;

    public EncryptionService(IConfiguration config)
    {
        _key = Environment.GetEnvironmentVariable("OTRADE_ENCRYPTION_KEY")
            ?? config["Security:EncryptionKey"]
            ?? throw new Exception("Encryption key not configured");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return "";

        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(_key));
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = Convert.ToBase64String(aes.IV) + "." + Convert.ToBase64String(cipherBytes);

        return "ENC:" + result;
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
            return "";

        if (!encryptedText.StartsWith("ENC:"))
            return encryptedText;

        var clean = encryptedText.Substring(4);
        var parts = clean.Split('.');

        var iv = Convert.FromBase64String(parts[0]);
        var cipherBytes = Convert.FromBase64String(parts[1]);

        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(_key));
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();

        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}