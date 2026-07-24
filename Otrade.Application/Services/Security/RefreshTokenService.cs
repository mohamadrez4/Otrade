using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Otrade.Application.Common;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services.Security;

public sealed class RefreshTokenIssueResult
{
    public string RawToken { get; set; }
        = string.Empty;

    public DateTime ExpiresAt { get; set; }
}

public sealed class RefreshTokenValidationResult
{
    public User User { get; set; }
        = null!;
}

public class RefreshTokenService
{
    private const int RefreshTokenByteLength =
        64;

    private readonly OtradeDbContext _context;

    private readonly int
        _refreshTokenExpireDays;

    public RefreshTokenService(
        OtradeDbContext context,
        IConfiguration configuration)
    {
        _context =
            context;

        var configuredDays =
            configuration.GetValue<int?>(
                "JwtSettings:RefreshTokenExpireDays")
            ?? 30;

        _refreshTokenExpireDays =
            Math.Clamp(
                configuredDays,
                1,
                90);
    }

    public async Task<
        ApiResponse<RefreshTokenIssueResult>>
        CreateSessionAsync(
            long userId,
            string? clientIp,
            string? userAgent)
    {
        var user =
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (
            user == null ||
            !user.EmailConfirmed
        )
        {
            return ResponseFactory
                .Fail<RefreshTokenIssueResult>(
                    "User session could not be created.");
        }

        var rawToken =
            GenerateRawToken();

        var tokenHash =
            HashToken(
                rawToken);

        var now =
            DateTime.UtcNow;

        var expiresAt =
            now.AddDays(
                _refreshTokenExpireDays);

        var refreshToken =
            new RefreshToken
            {
                UserId =
                    user.UserId,

                TokenHash =
                    tokenHash,

                TokenVersion =
                    Math.Max(
                        1,
                        user.AuthTokenVersion),

                CreatedAt =
                    now,

                ExpiresAt =
                    expiresAt,

                LastUsedAt =
                    null,

                RevokedAt =
                    null,

                CreatedByIp =
                    NormalizeIp(
                        clientIp),

                LastUsedByIp =
                    null,

                UserAgent =
                    NormalizeUserAgent(
                        userAgent)
            };

        _context.RefreshTokens.Add(
            refreshToken);

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            new RefreshTokenIssueResult
            {
                RawToken =
                    rawToken,

                ExpiresAt =
                    expiresAt
            },
            "Refresh session created.");
    }

    public async Task<
        ApiResponse<RefreshTokenValidationResult>>
        ValidateSessionAsync(
            string? rawToken,
            string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(
                rawToken))
        {
            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "Refresh session not found.");
        }

        var tokenHash =
            HashToken(
                rawToken.Trim());

        var token =
            await _context.RefreshTokens
                .AsNoTracking()
                .Include(x =>
                    x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash ==
                    tokenHash);

        if (token == null)
        {
            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "Refresh session is invalid.");
        }

        var now =
            DateTime.UtcNow;

        if (
            token.RevokedAt.HasValue ||
            token.ExpiresAt <= now
        )
        {
            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "Refresh session has expired.");
        }

        if (
            token.User == null ||
            !token.User.EmailConfirmed
        )
        {
            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "User session is unavailable.");
        }

        /*
         * وقتی Password یا 2FA Reset شود،
         * AuthTokenVersion افزایش پیدا می‌کند.
         * در نتیجه Refresh Tokenهای قبلی هم نامعتبرند.
         */
        if (
            token.TokenVersion !=
            token.User.AuthTokenVersion
        )
        {
            await _context.RefreshTokens
                .Where(x =>
                    x.RefreshTokenId ==
                    token.RefreshTokenId)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            x => x.RevokedAt,
                            (DateTime?)now));

            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "Refresh session has been revoked.");
        }

        /*
         * Update اتمیک برای ثبت آخرین استفاده.
         */
        var updatedRows =
            await _context.RefreshTokens
                .Where(x =>
                    x.RefreshTokenId ==
                    token.RefreshTokenId &&
                    x.RevokedAt == null &&
                    x.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                x => x.LastUsedAt,
                                (DateTime?)now)
                            .SetProperty(
                                x => x.LastUsedByIp,
                                NormalizeIp(
                                    clientIp)));

        if (updatedRows != 1)
        {
            return ResponseFactory
                .Fail<RefreshTokenValidationResult>(
                    "Refresh session is no longer valid.");
        }

        return ResponseFactory.Success(
            new RefreshTokenValidationResult
            {
                User =
                    token.User
            },
            "Refresh session validated.");
    }

    public async Task RevokeSessionAsync(
        string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(
                rawToken))
        {
            return;
        }

        var tokenHash =
            HashToken(
                rawToken.Trim());

        var now =
            DateTime.UtcNow;

        await _context.RefreshTokens
            .Where(x =>
                x.TokenHash == tokenHash &&
                x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        x => x.RevokedAt,
                        (DateTime?)now));
    }

    public async Task RevokeAllUserSessionsAsync(
        long userId)
    {
        var now =
            DateTime.UtcNow;

        await _context.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        x => x.RevokedAt,
                        (DateTime?)now));
    }

    private static string GenerateRawToken()
    {
        var randomBytes =
            new byte[
                RefreshTokenByteLength
            ];

        RandomNumberGenerator.Fill(
            randomBytes);

        try
        {
            return Convert
                .ToBase64String(
                    randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                randomBytes);
        }
    }

    private static string HashToken(
        string rawToken)
    {
        var rawBytes =
            Encoding.UTF8.GetBytes(
                rawToken);

        try
        {
            var hash =
                SHA256.HashData(
                    rawBytes);

            return Convert
                .ToHexString(
                    hash)
                .ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                rawBytes);
        }
    }

    private static string NormalizeIp(
        string? ipAddress)
    {
        var value =
            string.IsNullOrWhiteSpace(
                ipAddress)
                ? "unknown"
                : ipAddress.Trim();

        return value.Length <= 64
            ? value
            : value[..64];
    }

    private static string NormalizeUserAgent(
        string? userAgent)
    {
        var value =
            string.IsNullOrWhiteSpace(
                userAgent)
                ? "unknown"
                : userAgent.Trim();

        return value.Length <= 500
            ? value
            : value[..500];
    }
}