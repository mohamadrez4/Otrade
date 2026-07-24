using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Security;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using QRCoder;

namespace Otrade.Application.Services.Security;

public class TwoFactorAuthenticationService
{
    private const int SecretLengthBytes =
        20;

    private const int SetupLifetimeMinutes =
        15;

    private const int TotpStepSeconds =
        30;

    private const int TotpDigits =
        6;

    private const int RecoveryCodeCount =
        8;

    private const int RecoveryCodeLength =
        12;
    private const int LoginChallengeLifetimeMinutes =
        5;

    private const int LoginChallengeMaxAttempts =
        5;

    private const int LoginChallengeTokenLengthBytes =
        32;
    /*
     * کاراکترهای مبهم مثل:
     * O, 0, I, 1, L
     * عمداً حذف شده‌اند.
     */
    private const string RecoveryAlphabet =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly OtradeDbContext _context;

    private readonly TotpSecretProtector
        _secretProtector;

    private readonly ILogger<TwoFactorAuthenticationService>
        _logger;

    private readonly string _issuer;

    public TwoFactorAuthenticationService(
        OtradeDbContext context,
        TotpSecretProtector secretProtector,
        IConfiguration configuration,
        ILogger<TwoFactorAuthenticationService> logger)
    {
        _context = context;
        _secretProtector = secretProtector;
        _logger = logger; _issuer =
            configuration["Security:TotpIssuer"]?.Trim() ?? "Otrade";
        if (string.IsNullOrWhiteSpace(_issuer))
        {
            _issuer = "Otrade";
        }
    }

    public async Task<ApiResponse<TwoFactorStatusResponse>>GetStatusAsync(long userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.IsTotpEnabled,
                x.TotpEnabledAt,
                x.TotpSecretEncrypted,
                x.TotpSetupCreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return ResponseFactory
                .Fail<TwoFactorStatusResponse>(
                    "User not found.");
        }

        var now =
            DateTime.UtcNow;

        var setupExpiresAt =
            user.TotpSetupCreatedAt?
                .AddMinutes(
                    SetupLifetimeMinutes);

        var hasPendingSetup =
            !user.IsTotpEnabled &&
            !string.IsNullOrWhiteSpace(
                user.TotpSecretEncrypted) &&
            setupExpiresAt.HasValue &&
            setupExpiresAt.Value > now;

        var remainingRecoveryCodes = 0;

        if (user.IsTotpEnabled)
        {
            remainingRecoveryCodes =
                await _context.UserRecoveryCodes
                    .CountAsync(x =>
                        x.UserId == userId &&
                        x.UsedAt == null);
        }

        var response =
            new TwoFactorStatusResponse
            {
                IsEnabled =
                    user.IsTotpEnabled,

                EnabledAt =
                    user.TotpEnabledAt,

                HasPendingSetup =
                    hasPendingSetup,

                SetupExpiresAt =
                    hasPendingSetup
                        ? setupExpiresAt
                        : null,

                RecoveryCodesRemaining =
                    remainingRecoveryCodes
            };

        return ResponseFactory.Success(
            response);
    }

    public async Task<ApiResponse<TwoFactorSetupResponse>>CreateSetupAsync(long userId)
    {
        var user = await _context.Users
            .Include(x =>
                x.RecoveryCodes)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<TwoFactorSetupResponse>(
                    "User not found.");
        }

        if (user.IsTotpEnabled)
        {
            return ResponseFactory
                .Fail<TwoFactorSetupResponse>(
                    "Google Authenticator is already enabled.");
        }
        var now =
            DateTime.UtcNow;

        /*
         * اگر Setup قبلی هنوز معتبر است،
         * همان Secret قبلی برگردانده می‌شود.
         *
         * در نتیجه Refresh صفحه یا درخواست دوباره
         * باعث باطل‌شدن QR اسکن‌شده نمی‌شود.
         */
        if (
            !string.IsNullOrWhiteSpace(
                user.TotpSecretEncrypted) &&
            user.TotpSetupCreatedAt.HasValue
        )
        {
            var existingExpiresAt =
                user.TotpSetupCreatedAt.Value
                    .AddMinutes(
                        SetupLifetimeMinutes);

            if (existingExpiresAt > now)
            {
                if (!TryDecryptUserSecret(
                        user.UserId,
                        user.TotpSecretEncrypted,
                        out var existingSecretKey))
                {
                    return ResponseFactory
                        .Fail<TwoFactorSetupResponse>(
                            "Two-factor authentication setup is unavailable.");
                }

                var existingAccountName =
                    user.Email.Trim();

                var existingOtpAuthUri =
                    BuildOtpAuthUri(
                        existingAccountName,
                        existingSecretKey);

                var existingResponse =
                    new TwoFactorSetupResponse
                    {
                        QrCodeDataUrl =
                            BuildQrCodeDataUrl(
                                existingOtpAuthUri),

                        ManualEntryKey =
                            FormatManualEntryKey(
                                existingSecretKey),

                        Issuer =
                            _issuer,

                        AccountName =
                            existingAccountName,

                        ExpiresAt =
                            existingExpiresAt
                    };

                return ResponseFactory.Success(
                    existingResponse,
                    "Existing Google Authenticator setup loaded.");
            }
        }
        var secretBytes =
            new byte[SecretLengthBytes];

        using (
            var random =
                RandomNumberGenerator.Create()
        )
        {
            random.GetBytes(
                secretBytes);
        }

        string secretKey;

        try
        {
            secretKey =
                Base32Encoding.ToString(
                    secretBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                secretBytes);
        }


        user.TotpSecretEncrypted =
            _secretProtector.Protect(
                secretKey);

        user.TotpSetupCreatedAt =
            now;

        user.TotpEnabledAt =
            null;

        user.LastAcceptedTotpStep =
            null;

        /*
         * اگر Setup قبلی ناقص مانده باشد،
         * Recovery Code قدیمی حذف می‌شود.
         */
        if (user.RecoveryCodes.Count > 0)
        {
            _context.UserRecoveryCodes
                .RemoveRange(
                    user.RecoveryCodes);
        }

        await _context.SaveChangesAsync();

        var accountName =
            user.Email.Trim();

        var otpAuthUri =
            BuildOtpAuthUri(
                accountName,
                secretKey);

        var qrCodeDataUrl =
            BuildQrCodeDataUrl(
                otpAuthUri);

        var response =
            new TwoFactorSetupResponse
            {
                QrCodeDataUrl =
                    qrCodeDataUrl,

                ManualEntryKey =
                    FormatManualEntryKey(
                        secretKey),

                Issuer =
                    _issuer,

                AccountName =
                    accountName,

                ExpiresAt =
                    now.AddMinutes(
                        SetupLifetimeMinutes)
            };

        return ResponseFactory.Success(
            response,
            "Google Authenticator setup created.");
    }

    public async Task<ApiResponse<TwoFactorEnableResponse>>EnableAsync(long userId,VerifyTwoFactorSetupRequest request)
    {
        var normalizedCode =
            NormalizeTotpCode(
                request?.Code);

        if (!IsValidTotpCode(
                normalizedCode))
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "Please enter a valid 6-digit authentication code.");
        }

        var user = await _context.Users
            .Include(x =>
                x.RecoveryCodes)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "User not found.");
        }

        if (user.IsTotpEnabled)
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "Google Authenticator is already enabled.");
        }

        if (
            string.IsNullOrWhiteSpace(
                user.TotpSecretEncrypted) ||
            !user.TotpSetupCreatedAt.HasValue
        )
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "Create a Google Authenticator setup first.");
        }

        var now =
            DateTime.UtcNow;

        var expiresAt =
            user.TotpSetupCreatedAt.Value
                .AddMinutes(
                    SetupLifetimeMinutes);

        if (expiresAt <= now)
        {
            user.TotpSecretEncrypted =
                null;

            user.TotpSetupCreatedAt =
                null;

            await _context.SaveChangesAsync();

            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "The setup has expired. Create a new setup.");
        }

        if (!TryDecryptUserSecret(
                user.UserId,
                user.TotpSecretEncrypted,
                out var secretKey))
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "Two-factor authentication is unavailable. Contact support.");
        }

        var isValid =
            VerifyTotpCode(
                secretKey,
                normalizedCode,
                out var matchedStep);

        if (!isValid)
        {
            return ResponseFactory
                .Fail<TwoFactorEnableResponse>(
                    "Invalid Google Authenticator code.");
        }

        var recoveryCodes =
            ReplaceRecoveryCodes(
                user,
                now);

        user.IsTotpEnabled =
            true;

        user.TotpEnabledAt =
            now;

        user.TotpSetupCreatedAt =
            null;

        /*
         * کدی که برای فعال‌سازی استفاده شده،
         * دوباره قابل استفاده نخواهد بود.
         */
        user.LastAcceptedTotpStep =
            matchedStep;

        user.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response =
            new TwoFactorEnableResponse
            {
                IsEnabled =
                    true,

                EnabledAt =
                    now,

                RecoveryCodes =
                    recoveryCodes
            };

        return ResponseFactory.Success(
            response,
            "Google Authenticator enabled successfully.");
    }

    public async Task<ApiResponse<bool>>VerifyTotpAsync(long userId,string? code,bool preventReplay = true)
    {
        var normalizedCode =
            NormalizeTotpCode(code);

        if (!IsValidTotpCode(
                normalizedCode))
        {
            return ResponseFactory.Fail<bool>(
                "Please enter a valid 6-digit authentication code.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.IsTotpEnabled,
                x.TotpSecretEncrypted
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return ResponseFactory.Fail<bool>(
                "User not found.");
        }

        if (
            !user.IsTotpEnabled ||
            string.IsNullOrWhiteSpace(
                user.TotpSecretEncrypted)
        )
        {
            return ResponseFactory.Fail<bool>(
                "Google Authenticator is not enabled.");
        }

        if (!TryDecryptUserSecret(
                user.UserId,
                user.TotpSecretEncrypted,
                out var secretKey))
        {
            return ResponseFactory.Fail<bool>(
                "Two-factor authentication is unavailable. Contact support.");
        }

        var isValid =
            VerifyTotpCode(
                secretKey,
                normalizedCode,
                out var matchedStep);

        if (!isValid)
        {
            return ResponseFactory.Fail<bool>(
                "Invalid Google Authenticator code.");
        }

        if (preventReplay)
        {
            /*
             * این Update اتمیک است.
             * اگر دو درخواست هم‌زمان همان کد را ارسال کنند،
             * فقط یکی از آن‌ها موفق خواهد شد.
             */
            var updatedRows =
                await _context.Users
                    .Where(x =>
                        x.UserId == userId &&
                        x.IsTotpEnabled &&
                        (
                            x.LastAcceptedTotpStep == null ||
                            x.LastAcceptedTotpStep.Value <
                            matchedStep
                        ))
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                x =>
                                    x.LastAcceptedTotpStep,
                                (long?)matchedStep));

            if (updatedRows == 0)
            {
                return ResponseFactory.Fail<bool>(
                    "This authentication code has already been used or is no longer valid.");
            }
        }

        return ResponseFactory.Success(
            true,
            "Google Authenticator code verified.");
    }

    public async Task<ApiResponse<bool>> DisableAsync(long userId,DisableTwoFactorRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request?.Password))
        {
            return ResponseFactory.Fail<bool>(
                "Password is required.");
        }

        if (string.IsNullOrWhiteSpace(
                request.Code))
        {
            return ResponseFactory.Fail<bool>(
                "Authentication code is required.");
        }

        var user = await _context.Users
            .Include(x =>
                x.RecoveryCodes)
            .Include(x =>
                x.TwoFactorLoginChallenges)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory.Fail<bool>(
                "User not found.");
        }

        if (!user.IsTotpEnabled)
        {
            return ResponseFactory.Fail<bool>(
                "Google Authenticator is not enabled.");
        }

        var passwordIsValid =
            BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

        if (!passwordIsValid)
        {
            return ResponseFactory.Fail<bool>(
                "Current password is incorrect.");
        }

        var normalizedTotpCode =
            NormalizeTotpCode(
                request.Code);

        var authenticated = false;

        if (IsValidTotpCode(
                normalizedTotpCode))
        {
            var verification =
                await VerifyTotpAsync(
                    userId,
                    normalizedTotpCode,
                    preventReplay: true);

            authenticated =
                verification.Success;
        }
        else
        {
            authenticated =
                TryUseRecoveryCode(
                    user,
                    request.Code,
                    DateTime.UtcNow);
        }

        if (!authenticated)
        {
            return ResponseFactory.Fail<bool>(
                "Invalid authentication or recovery code.");
        }

        user.IsTotpEnabled =
            false;

        user.TotpSecretEncrypted =
            null;

        user.TotpSetupCreatedAt =
            null;

        user.TotpEnabledAt =
            null;

        user.LastAcceptedTotpStep =
            null;

        user.UpdatedAt =
            DateTime.UtcNow;

        if (user.RecoveryCodes.Count > 0)
        {
            _context.UserRecoveryCodes
                .RemoveRange(
                    user.RecoveryCodes);
        }

        if (
            user.TwoFactorLoginChallenges.Count >
            0
        )
        {
            _context.TwoFactorLoginChallenges
                .RemoveRange(
                    user.TwoFactorLoginChallenges);
        }

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            true,
            "Google Authenticator disabled successfully.");
    }

    public async Task<ApiResponse<RecoveryCodesResponse>>RegenerateRecoveryCodesAsync(long userId,RegenerateRecoveryCodesRequest request)
    {
        var verification =
            await VerifyTotpAsync(
                userId,
                request?.Code,
                preventReplay: true);

        if (!verification.Success)
        {
            return ResponseFactory
                .Fail<RecoveryCodesResponse>(
                    verification.Message);
        }

        var user = await _context.Users
            .Include(x =>
                x.RecoveryCodes)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<RecoveryCodesResponse>(
                    "User not found.");
        }

        if (!user.IsTotpEnabled)
        {
            return ResponseFactory
                .Fail<RecoveryCodesResponse>(
                    "Google Authenticator is not enabled.");
        }

        var recoveryCodes =
            ReplaceRecoveryCodes(
                user,
                DateTime.UtcNow);

        await _context.SaveChangesAsync();

        var response =
            new RecoveryCodesResponse
            {
                RecoveryCodes =
                    recoveryCodes
            };

        return ResponseFactory.Success(
            response,
            "New recovery codes generated successfully.");
    }
    public async Task<ApiResponse<TwoFactorLoginChallengeResponse>>CreateLoginChallengeAsync(long userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.IsTotpEnabled
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return ResponseFactory
                .Fail<TwoFactorLoginChallengeResponse>(
                    "User not found.");
        }

        if (!user.IsTotpEnabled)
        {
            return ResponseFactory
                .Fail<TwoFactorLoginChallengeResponse>(
                    "Google Authenticator is not enabled.");
        }

        var now =
            DateTime.UtcNow;

        /*
         * در هر لحظه فقط یک Challenge فعال
         * برای هر کاربر باقی می‌ماند.
         */
        await _context.TwoFactorLoginChallenges
            .Where(x =>
                x.UserId == userId &&
                !x.IsUsed)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(
                            x => x.IsUsed,
                            true)
                        .SetProperty(
                            x => x.UsedAt,
                            (DateTime?)now));

        string challengeToken;
        string tokenHash;

        do
        {
            challengeToken =
                GenerateLoginChallengeToken();

            tokenHash =
                HashLoginChallengeToken(
                    challengeToken);
        }
        while (
            await _context
                .TwoFactorLoginChallenges
                .AnyAsync(x =>
                    x.TokenHash == tokenHash)
        );

        var expiresAt =
            now.AddMinutes(
                LoginChallengeLifetimeMinutes);

        var challenge =
            new TwoFactorLoginChallenge
            {
                UserId =
                    userId,

                TokenHash =
                    tokenHash,

                Attempts =
                    0,

                IsUsed =
                    false,

                CreatedAt =
                    now,

                ExpiresAt =
                    expiresAt,

                UsedAt =
                    null
            };

        _context.TwoFactorLoginChallenges.Add(
            challenge);

        await _context.SaveChangesAsync();

        var response =
            new TwoFactorLoginChallengeResponse
            {
                ChallengeToken =
                    challengeToken,

                ExpiresAt =
                    expiresAt
            };

        return ResponseFactory.Success(
            response,
            "Two-factor authentication is required.");
    }

    public async Task<ApiResponse<long>>VerifyLoginChallengeAsync(string? challengeToken,string? code)
    {
        if (string.IsNullOrWhiteSpace(
                challengeToken))
        {
            return ResponseFactory.Fail<long>(
                "The login verification request is invalid or expired.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return ResponseFactory.Fail<long>(
                "Authentication code is required.");
        }

        var tokenHash =
            HashLoginChallengeToken(
                challengeToken.Trim());

        var challenge =
            await _context
                .TwoFactorLoginChallenges
                .AsNoTracking()
                .Include(x =>
                    x.User)
                .ThenInclude(x =>
                    x.RecoveryCodes)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash);

        if (challenge == null)
        {
            return ResponseFactory.Fail<long>(
                "The login verification request is invalid or expired.");
        }

        var now =
            DateTime.UtcNow;

        if (
            challenge.IsUsed ||
            challenge.ExpiresAt <= now
        )
        {
            if (!challenge.IsUsed)
            {
                await _context
                    .TwoFactorLoginChallenges
                    .Where(x =>
                        x.ChallengeId ==
                        challenge.ChallengeId &&
                        !x.IsUsed)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(
                                    x => x.IsUsed,
                                    true)
                                .SetProperty(
                                    x => x.UsedAt,
                                    (DateTime?)now));
            }

            return ResponseFactory.Fail<long>(
                "The login verification request is invalid or expired.");
        }

        if (
            challenge.Attempts >=
            LoginChallengeMaxAttempts
        )
        {
            await _context
                .TwoFactorLoginChallenges
                .Where(x =>
                    x.ChallengeId ==
                    challenge.ChallengeId &&
                    !x.IsUsed)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                x => x.IsUsed,
                                true)
                            .SetProperty(
                                x => x.UsedAt,
                                (DateTime?)now));

            return ResponseFactory.Fail<long>(
                "Too many invalid attempts. Please login again.");
        }

        if (!challenge.User.IsTotpEnabled)
        {
            await _context
                .TwoFactorLoginChallenges
                .Where(x =>
                    x.ChallengeId ==
                    challenge.ChallengeId &&
                    !x.IsUsed)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                x => x.IsUsed,
                                true)
                            .SetProperty(
                                x => x.UsedAt,
                                (DateTime?)now));

            return ResponseFactory.Fail<long>(
                "Google Authenticator is not enabled.");
        }

        var authenticated =
            false;

        var normalizedTotpCode =
            NormalizeTotpCode(code);

        /*
         * کد 6 رقمی به‌عنوان TOTP بررسی می‌شود.
         * سایر مقادیر به‌عنوان Recovery Code.
         */
        if (IsValidTotpCode(
                normalizedTotpCode))
        {
            var verification =
                await VerifyTotpAsync(
                    challenge.UserId,
                    normalizedTotpCode,
                    preventReplay: true);

            authenticated =
                verification.Success;
        }
        else
        {
            authenticated =
                await TryUseRecoveryCodeForLoginAsync(
                    challenge.User,
                    code,
                    now);
        }

        var nextAttempt =
            challenge.Attempts + 1;

        if (!authenticated)
        {
            if (
                nextAttempt >=
                LoginChallengeMaxAttempts
            )
            {
                await _context
                    .TwoFactorLoginChallenges
                    .Where(x =>
                        x.ChallengeId ==
                        challenge.ChallengeId &&
                        !x.IsUsed &&
                        x.Attempts ==
                        challenge.Attempts)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(
                                    x => x.Attempts,
                                    nextAttempt)
                                .SetProperty(
                                    x => x.IsUsed,
                                    true)
                                .SetProperty(
                                    x => x.UsedAt,
                                    (DateTime?)now));

                return ResponseFactory.Fail<long>(
                    "Too many invalid attempts. Please login again.");
            }

            await _context
                .TwoFactorLoginChallenges
                .Where(x =>
                    x.ChallengeId ==
                    challenge.ChallengeId &&
                    !x.IsUsed &&
                    x.Attempts ==
                    challenge.Attempts)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            x => x.Attempts,
                            nextAttempt));

            return ResponseFactory.Fail<long>(
                "Invalid authentication code.");
        }

        /*
         * Challenge به‌صورت اتمیک مصرف می‌شود.
         * یک Challenge نمی‌تواند دو بار JWT صادر کند.
         */
        var claimedRows =
            await _context
                .TwoFactorLoginChallenges
                .Where(x =>
                    x.ChallengeId ==
                    challenge.ChallengeId &&
                    !x.IsUsed &&
                    x.ExpiresAt > now &&
                    x.Attempts ==
                    challenge.Attempts)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                x => x.Attempts,
                                nextAttempt)
                            .SetProperty(
                                x => x.IsUsed,
                                true)
                            .SetProperty(
                                x => x.UsedAt,
                                (DateTime?)now));

        if (claimedRows != 1)
        {
            return ResponseFactory.Fail<long>(
                "The login verification request is invalid or expired.");
        }

        return ResponseFactory.Success(
            challenge.UserId,
            "Two-factor authentication verified.");
    }
    private async Task<bool>
    TryUseRecoveryCodeForLoginAsync(User user,string? suppliedCode,DateTime usedAt)
    {
        var normalizedCode =
            NormalizeRecoveryCode(
                suppliedCode);

        if (
            normalizedCode.Length !=
            RecoveryCodeLength
        )
        {
            return false;
        }

        foreach (
            var recoveryCode in
            user.RecoveryCodes
                .Where(x =>
                    x.UsedAt == null)
        )
        {
            var matches =
                BCrypt.Net.BCrypt.Verify(
                    normalizedCode,
                    recoveryCode.CodeHash);

            if (!matches)
            {
                continue;
            }

            /*
             * شرط UsedAt == null باعث می‌شود
             * Recovery Code فقط یک‌بار مصرف شود.
             */
            var updatedRows =
                await _context
                    .UserRecoveryCodes
                    .Where(x =>
                        x.UserRecoveryCodeId ==
                        recoveryCode
                            .UserRecoveryCodeId &&
                        x.UsedAt == null)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                x => x.UsedAt,
                                (DateTime?)usedAt));

            return updatedRows == 1;
        }

        return false;
    }
    private static string GenerateLoginChallengeToken()
    {
        var tokenBytes =
            new byte[
                LoginChallengeTokenLengthBytes
            ];

        using (
            var random =
                RandomNumberGenerator.Create()
        )
        {
            random.GetBytes(
                tokenBytes);
        }

        try
        {
            /*
             * Base64 URL Safe
             */
            return Convert
                .ToBase64String(
                    tokenBytes)
                .TrimEnd('=')
                .Replace(
                    '+',
                    '-')
                .Replace(
                    '/',
                    '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                tokenBytes);
        }
    }

    private static string HashLoginChallengeToken(string challengeToken)
    {
        var tokenBytes =
            Encoding.UTF8.GetBytes(
                challengeToken);

        try
        {
            using var sha256 =
                SHA256.Create();

            var hashBytes =
                sha256.ComputeHash(
                    tokenBytes);

            return Convert
                .ToHexString(
                    hashBytes)
                .ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                tokenBytes);
        }
    }
    private bool TryDecryptUserSecret(
        long userId,
        string protectedSecret,
        out string secretKey)
    {
        try
        {
            secretKey =
                _secretProtector.Unprotect(
                    protectedSecret);

            return !string.IsNullOrWhiteSpace(
                secretKey);
        }
        catch (
            Exception exception
        )
        when (
            exception is CryptographicException ||
            exception is FormatException ||
            exception is InvalidOperationException ||
            exception is ArgumentException
        )
        {
            /*
             * خود Secret یا مقدار رمزنگاری‌شده
             * هرگز داخل Log نوشته نمی‌شود.
             */
            _logger.LogError(
                exception,
                "Could not decrypt TOTP secret for UserId {UserId}.",
                userId);

            secretKey =
                string.Empty;

            return false;
        }
    }

    private static bool VerifyTotpCode(
        string secretKey,
        string normalizedCode,
        out long matchedStep)
    {
        matchedStep =
            0;

        var secretBytes =
            Base32Encoding.ToBytes(
                secretKey);

        try
        {
            var totp =
                new Totp(
                    secretBytes,
                    step: TotpStepSeconds,
                    mode: OtpHashMode.Sha1,
                    totpSize: TotpDigits);
            return totp.VerifyTotp(
                normalizedCode,
                out matchedStep,
                window:
                    new VerificationWindow(
                        previous: 1,
                        future: 1));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                secretBytes);
        }
    }

    private string BuildOtpAuthUri(
        string accountName,
        string secretKey)
    {
        var label =
            Uri.EscapeDataString(
                $"{_issuer}:{accountName}");

        var encodedIssuer =
            Uri.EscapeDataString(
                _issuer);

        var encodedSecret =
            Uri.EscapeDataString(
                secretKey);

        return
            $"otpauth://totp/{label}" +
            $"?secret={encodedSecret}" +
            $"&issuer={encodedIssuer}" +
            "&algorithm=SHA1" +
            $"&digits={TotpDigits}" +
            $"&period={TotpStepSeconds}";
    }

    private static string BuildQrCodeDataUrl(
        string otpAuthUri)
    {
        using var qrGenerator =
            new QRCodeGenerator();

        using var qrCodeData =
            qrGenerator.CreateQrCode(
                otpAuthUri,
                QRCodeGenerator.ECCLevel.Q);

        var svgQrCode =
            new SvgQRCode(
                qrCodeData);

        var svg =
            svgQrCode.GetGraphic(8);

        var svgBytes =
            Encoding.UTF8.GetBytes(
                svg);

        return
            "data:image/svg+xml;base64," +
            Convert.ToBase64String(
                svgBytes);
    }

    private List<string> ReplaceRecoveryCodes(
        User user,
        DateTime createdAt)
    {
        if (user.RecoveryCodes.Count > 0)
        {
            _context.UserRecoveryCodes
                .RemoveRange(
                    user.RecoveryCodes);
        }

        var displayCodes =
            new List<string>();

        var uniqueNormalizedCodes =
            new HashSet<string>(
                StringComparer.Ordinal);

        while (
            displayCodes.Count <
            RecoveryCodeCount
        )
        {
            var normalizedCode =
                GenerateRecoveryCode();

            if (!uniqueNormalizedCodes.Add(
                    normalizedCode))
            {
                continue;
            }

            var displayCode =
                FormatRecoveryCode(
                    normalizedCode);

            var entity =
                new UserRecoveryCode
                {
                    UserId =
                        user.UserId,

                    CodeHash =
                        BCrypt.Net.BCrypt
                            .HashPassword(
                                normalizedCode,
                                workFactor: 12),

                    CreatedAt =
                        createdAt,

                    UsedAt =
                        null
                };

            _context.UserRecoveryCodes.Add(
                entity);

            displayCodes.Add(
                displayCode);
        }

        return displayCodes;
    }

    private static bool TryUseRecoveryCode(
        User user,
        string? suppliedCode,
        DateTime usedAt)
    {
        var normalizedCode =
            NormalizeRecoveryCode(
                suppliedCode);

        if (
            normalizedCode.Length !=
            RecoveryCodeLength
        )
        {
            return false;
        }

        foreach (
            var recoveryCode in
            user.RecoveryCodes
                .Where(x =>
                    x.UsedAt == null)
        )
        {
            if (
                BCrypt.Net.BCrypt.Verify(
                    normalizedCode,
                    recoveryCode.CodeHash)
            )
            {
                recoveryCode.UsedAt =
                    usedAt;

                return true;
            }
        }

        return false;
    }

    private static string GenerateRecoveryCode()
    {
        var characters =
            new char[RecoveryCodeLength];

        for (
            var index = 0;
            index < characters.Length;
            index++
        )
        {
            var randomIndex =
                RandomNumberGenerator.GetInt32(
                    RecoveryAlphabet.Length);

            characters[index] =
                RecoveryAlphabet[
                    randomIndex
                ];
        }

        return new string(
            characters);
    }

    private static string FormatRecoveryCode(
        string normalizedCode)
    {
        return string.Join(
            "-",
            normalizedCode
                .Chunk(4)
                .Select(chunk =>
                    new string(chunk)));
    }

    private static string FormatManualEntryKey(
        string secretKey)
    {
        return string.Join(
            " ",
            secretKey
                .Chunk(4)
                .Select(chunk =>
                    new string(chunk)));
    }

    private static string NormalizeTotpCode(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return new string(
            code
                .Where(character =>
                    !char.IsWhiteSpace(character))
                .ToArray());
    }

    private static bool IsValidTotpCode(
        string code)
    {
        return
            code.Length == TotpDigits &&
            code.All(char.IsDigit);
    }

    private static string NormalizeRecoveryCode(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return new string(
            code
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }
}