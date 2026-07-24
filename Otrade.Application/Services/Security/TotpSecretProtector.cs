using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Otrade.Application.Services.Security;

public class TotpSecretProtector
{
    private const string ValuePrefix =
        "TOTP1:";

    private const int NonceSize =
        12;

    private const int TagSize =
        16;

    private static readonly byte[] AssociatedData =
        Encoding.UTF8.GetBytes(
            "Otrade.TOTP.Secret.v1");

    private readonly byte[] _encryptionKey;

    public TotpSecretProtector(
        IConfiguration configuration)
    {
        /*
         * مقدار از appsettings.json خوانده می‌شود.
         *
         * بعداً در Docker می‌توان همین مقدار را با:
         * Security__TotpEncryptionKey
         * Override کرد.
         */
        var configuredKey =
            configuration[
                "Security:TotpEncryptionKey"
            ];

        /*
         * این بخش فقط برای سازگاری با Environment Variable
         * قبلی نگه داشته شده است.
         */
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey =
                Environment.GetEnvironmentVariable(
                    "OTRADE_TOTP_ENCRYPTION_KEY");
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "TOTP encryption key is not configured. " +
                "Set Security:TotpEncryptionKey in appsettings.json.");
        }

        try
        {
            _encryptionKey =
                Convert.FromBase64String(
                    configuredKey.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Security:TotpEncryptionKey must be a valid Base64 value.",
                exception);
        }

        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException(
                "Security:TotpEncryptionKey must contain exactly 32 bytes.");
        }
    }

    public string Protect(
        string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException(
                "TOTP secret cannot be empty.",
                nameof(plainText));
        }

        var plainBytes =
            Encoding.UTF8.GetBytes(
                plainText);

        var cipherBytes =
            new byte[
                plainBytes.Length
            ];

        var nonce =
            new byte[
                NonceSize
            ];

        var tag =
            new byte[
                TagSize
            ];

        using var random =
            RandomNumberGenerator.Create();

        random.GetBytes(
            nonce);

        try
        {
            using var aes =
                new AesGcm(
                    _encryptionKey,
                    TagSize);

            aes.Encrypt(
                nonce,
                plainBytes,
                cipherBytes,
                tag,
                AssociatedData);

            return
                ValuePrefix +
                Convert.ToBase64String(nonce) +
                "." +
                Convert.ToBase64String(tag) +
                "." +
                Convert.ToBase64String(cipherBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);
        }
    }

    public string Unprotect(
        string protectedText)
    {
        if (string.IsNullOrWhiteSpace(
                protectedText))
        {
            throw new ArgumentException(
                "Protected TOTP secret cannot be empty.",
                nameof(protectedText));
        }

        if (!protectedText.StartsWith(
                ValuePrefix,
                StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "Invalid TOTP secret format.");
        }

        var cleanValue =
            protectedText[
                ValuePrefix.Length..
            ];

        var parts =
            cleanValue.Split(
                '.',
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (parts.Length != 3)
        {
            throw new CryptographicException(
                "Invalid TOTP secret payload.");
        }

        byte[] nonce;
        byte[] tag;
        byte[] cipherBytes;

        try
        {
            nonce =
                Convert.FromBase64String(
                    parts[0]);

            tag =
                Convert.FromBase64String(
                    parts[1]);

            cipherBytes =
                Convert.FromBase64String(
                    parts[2]);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                "Invalid TOTP secret encoding.",
                exception);
        }

        if (
            nonce.Length != NonceSize ||
            tag.Length != TagSize
        )
        {
            throw new CryptographicException(
                "Invalid TOTP encryption parameters.");
        }

        var plainBytes =
            new byte[
                cipherBytes.Length
            ];

        try
        {
            using var aes =
                new AesGcm(
                    _encryptionKey,
                    TagSize);

            aes.Decrypt(
                nonce,
                cipherBytes,
                tag,
                plainBytes,
                AssociatedData);

            return Encoding.UTF8.GetString(
                plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);
        }
    }
}