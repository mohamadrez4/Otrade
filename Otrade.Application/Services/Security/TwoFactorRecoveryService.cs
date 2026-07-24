using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OtpNet;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Security;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using QRCoder;

namespace Otrade.Application.Services.Security;

public class TwoFactorRecoveryService
{
    private const int SecretLengthBytes = 20;
    private const int TotpStepSeconds = 30;
    private const int TotpDigits = 6;

    private const int ReplacementLifetimeMinutes = 15;

    private const int RecoveryEmailCodeLifetimeMinutes = 15;
    private const int RecoveryRequestLifetimeDays = 7;
    private const int RecoveryMaxAttempts = 5;
    private const int RecoveryTokenLengthBytes = 32;

    private const int RecoveryCodeCount = 8;
    private const int RecoveryCodeLength = 12;

    private const int WithdrawalLockHoursAfterRecovery = 24;

    private const string RecoveryAlphabet =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly OtradeDbContext _context;
    private readonly TotpSecretProtector _secretProtector;
    private readonly TwoFactorAuthenticationService _twoFactorService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationQueue _notificationQueue;
    private readonly string _issuer;

    public TwoFactorRecoveryService(
        OtradeDbContext context,
        TotpSecretProtector secretProtector,
        TwoFactorAuthenticationService twoFactorService,
        IEmailTemplateService emailTemplateService,
        INotificationQueue notificationQueue,
        IConfiguration configuration)
    {
        _context = context;
        _secretProtector = secretProtector;
        _twoFactorService = twoFactorService;
        _emailTemplateService = emailTemplateService;
        _notificationQueue = notificationQueue;

        _issuer =
            configuration["Security:TotpIssuer"]?.Trim()
            ?? "Otrade";

        if (string.IsNullOrWhiteSpace(_issuer))
        {
            _issuer = "Otrade";
        }
    }

    // =========================================================
    // Authenticated replacement
    // =========================================================

    public async Task<ApiResponse<TwoFactorSetupResponse>>
        StartReplacementAsync(
            long userId,
            StartTwoFactorReplacementRequest request)
    {
        if (request == null)
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Invalid replacement request.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Authentication or recovery code is required.");
        }

        var user = await _context.Users
            .Include(x => x.RecoveryCodes)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "User not found.");
        }

        if (!user.IsTotpEnabled)
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Google Authenticator is not enabled.");
        }

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Current password is incorrect.");
        }

        var factorVerified =
            await VerifyCurrentFactorAsync(
                user,
                request.Code);

        if (!factorVerified)
        {
            return ResponseFactory.Fail<TwoFactorSetupResponse>(
                "Invalid authentication or recovery code.");
        }

        var pendingSecret =
            GenerateBase32Secret();

        var now =
            DateTime.UtcNow;

        user.PendingTotpSecretEncrypted =
            _secretProtector.Protect(
                pendingSecret);

        user.PendingTotpCreatedAt =
            now;

        user.UpdatedAt =
            DateTime.Now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            BuildSetupResponse(
                user.Email,
                pendingSecret,
                now.AddMinutes(
                    ReplacementLifetimeMinutes)),
            "New Google Authenticator setup created. " +
            "The current authenticator remains active until the new one is confirmed.");
    }

    public async Task<ApiResponse<TwoFactorReplacementResponse>>
        ConfirmReplacementAsync(
            long userId,
            ConfirmTwoFactorReplacementRequest request)
    {
        var normalizedCode =
            NormalizeTotpCode(
                request?.Code);

        if (!IsValidTotpCode(
                normalizedCode))
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "Enter a valid 6-digit code from the new authenticator.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var user = await _context.Users
            .Include(x => x.RecoveryCodes)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "User not found.");
        }

        if (!user.IsTotpEnabled)
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "Google Authenticator is not enabled.");
        }

        if (
            string.IsNullOrWhiteSpace(
                user.PendingTotpSecretEncrypted) ||
            !user.PendingTotpCreatedAt.HasValue
        )
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "Start the authenticator replacement first.");
        }

        var now =
            DateTime.UtcNow;

        var pendingExpiresAt =
            user.PendingTotpCreatedAt.Value
                .AddMinutes(
                    ReplacementLifetimeMinutes);

        if (pendingExpiresAt <= now)
        {
            user.PendingTotpSecretEncrypted =
                null;

            user.PendingTotpCreatedAt =
                null;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "The replacement setup has expired. Start again.");
        }

        string pendingSecret;

        try
        {
            pendingSecret =
                _secretProtector.Unprotect(
                    user.PendingTotpSecretEncrypted);
        }
        catch (
            Exception exception
        )
        when (
            exception is CryptographicException ||
            exception is InvalidOperationException ||
            exception is ArgumentException ||
            exception is FormatException
        )
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "The replacement setup is unavailable. Start again.");
        }

        var isValid =
            VerifyTotpCode(
                pendingSecret,
                normalizedCode,
                out var matchedStep);

        if (!isValid)
        {
            return ResponseFactory.Fail<TwoFactorReplacementResponse>(
                "Invalid code from the new Google Authenticator.");
        }

        var recoveryCodes =
            ReplaceRecoveryCodes(
                user,
                now);

        user.TotpSecretEncrypted =
            user.PendingTotpSecretEncrypted;

        user.PendingTotpSecretEncrypted =
            null;

        user.PendingTotpCreatedAt =
            null;

        user.TotpEnabledAt =
            now;

        user.TotpSetupCreatedAt =
            null;

        user.LastAcceptedTotpStep =
            matchedStep;
        user.LastWithdrawalTotpStep =
            null;       
        user.AuthTokenVersion =
            Math.Max(
                1,
                user.AuthTokenVersion + 1);

        user.UpdatedAt =
            DateTime.Now;

        await _context.TwoFactorLoginChallenges
            .Where(x =>
                x.UserId == user.UserId &&
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

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var response =
            new TwoFactorReplacementResponse
            {
                Replaced =
                    true,

                RequiresLoginAgain =
                    true,

                ReplacedAt =
                    now,

                RecoveryCodes =
                    recoveryCodes
            };

        return ResponseFactory.Success(
            response,
            "Google Authenticator replaced successfully. " +
            "All previous recovery codes are now invalid.");
    }

    // =========================================================
    // Lost-access recovery
    // =========================================================

    public async Task<ApiResponse<StartTwoFactorRecoveryResponse>>
        StartRecoveryAsync(
            StartTwoFactorRecoveryRequest request)
    {
        if (request == null)
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "Invalid recovery request.");
        }

        var challengeToken =
            request.ChallengeToken?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(
                challengeToken))
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "Start the login process again.");
        }

        var description =
            request.Description?.Trim()
            ?? string.Empty;

        if (
            description.Length < 10 ||
            description.Length > 1000
        )
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "Describe the access problem using 10 to 1000 characters.");
        }

        var challengeHash =
            HashPublicToken(
                challengeToken);

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var challenge =
            await _context.TwoFactorLoginChallenges
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash ==
                    challengeHash);

        var now =
            DateTime.UtcNow;

        if (
            challenge == null ||
            challenge.IsUsed ||
            challenge.ExpiresAt <= now ||
            !challenge.User.IsTotpEnabled
        )
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "The login verification request is invalid or expired.");
        }

        var hasPendingAdminReview =
            await _context.TwoFactorRecoveryRequests
                .AnyAsync(x =>
                    x.UserId ==
                    challenge.UserId &&
                    x.Status ==
                    TwoFactorRecoveryRequestStatus
                        .PendingAdminReview &&
                    x.ExpiresAt >
                    now);

        if (hasPendingAdminReview)
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "A recovery request is already waiting for admin review.");
        }

        var recentRequest =
            await _context.TwoFactorRecoveryRequests
                .AsNoTracking()
                .Where(x =>
                    x.UserId ==
                    challenge.UserId)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x =>
                    (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync();

        if (
            recentRequest.HasValue &&
            recentRequest.Value >
            now.AddMinutes(-2)
        )
        {
            return ResponseFactory.Fail<StartTwoFactorRecoveryResponse>(
                "Please wait before creating another recovery request.");
        }

        await _context.TwoFactorRecoveryRequests
            .Where(x =>
                x.UserId ==
                challenge.UserId &&
                x.Status ==
                TwoFactorRecoveryRequestStatus
                    .PendingEmailVerification)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        x => x.Status,
                        TwoFactorRecoveryRequestStatus
                            .Canceled));

        var recoveryToken =
            GenerateUrlSafeToken(
                RecoveryTokenLengthBytes);

        var emailCode =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000)
                .ToString();

        var emailCodeExpiresAt =
            now.AddMinutes(
                RecoveryEmailCodeLifetimeMinutes);

        var requestExpiresAt =
            now.AddDays(
                RecoveryRequestLifetimeDays);

        var recoveryRequest =
            new TwoFactorRecoveryRequest
            {
                UserId =
                    challenge.UserId,

                PublicTokenHash =
                    HashPublicToken(
                        recoveryToken),

                EmailCodeHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        emailCode,
                        workFactor: 12),

                Attempts =
                    0,

                Status =
                    TwoFactorRecoveryRequestStatus
                        .PendingEmailVerification,

                UserDescription =
                    description,

                AdminNote =
                    null,

                ReviewedByAdminId =
                    null,

                CreatedAt =
                    now,

                ExpiresAt =
                    requestExpiresAt,

                EmailCodeExpiresAt =
                    emailCodeExpiresAt,

                EmailVerifiedAt =
                    null,

                ReviewedAt =
                    null
            };

        _context.TwoFactorRecoveryRequests.Add(
            recoveryRequest);

        challenge.IsUsed =
            true;

        challenge.UsedAt =
            now;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var emailBody =
            _emailTemplateService
                .GetTwoFactorRecoveryCodeEmail(
                    emailCode,
                    RecoveryEmailCodeLifetimeMinutes);

        await _notificationQueue.QueueEmailAsync(
            challenge.User.Email,
            "Otrade Two-Factor Recovery Verification",
            emailBody);

        var response =
            new StartTwoFactorRecoveryResponse
            {
                RecoveryToken =
                    recoveryToken,

                MaskedEmail =
                    MaskEmail(
                        challenge.User.Email),

                EmailCodeExpiresAt =
                    emailCodeExpiresAt,

                RequestExpiresAt =
                    requestExpiresAt
            };

        return ResponseFactory.Success(
            response,
            "A verification code was sent to your email.");
    }

    public async Task<ApiResponse<TwoFactorRecoveryStatusResponse>>
        VerifyRecoveryEmailAsync(
            VerifyTwoFactorRecoveryEmailRequest request)
    {
        if (
            request == null ||
            string.IsNullOrWhiteSpace(
                request.RecoveryToken) ||
            string.IsNullOrWhiteSpace(
                request.Code)
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Invalid recovery verification request.");
        }

        var normalizedCode =
            NormalizeTotpCode(
                request.Code);

        if (!IsValidTotpCode(
                normalizedCode))
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Enter the 6-digit code sent to your email.");
        }

        var tokenHash =
            HashPublicToken(
                request.RecoveryToken.Trim());

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var recoveryRequest =
            await _context.TwoFactorRecoveryRequests
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.PublicTokenHash ==
                    tokenHash);

        var now =
            DateTime.UtcNow;

        if (recoveryRequest == null)
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "The recovery request is invalid or expired.");
        }

        if (
            recoveryRequest.Status !=
            TwoFactorRecoveryRequestStatus
                .PendingEmailVerification
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "This email verification step is no longer available.");
        }

        if (
            recoveryRequest.ExpiresAt <= now ||
            recoveryRequest.EmailCodeExpiresAt <= now
        )
        {
            recoveryRequest.Status =
                TwoFactorRecoveryRequestStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "The email verification code has expired.");
        }

        if (
            recoveryRequest.Attempts >=
            RecoveryMaxAttempts
        )
        {
            recoveryRequest.Status =
                TwoFactorRecoveryRequestStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Too many invalid attempts. Start recovery again.");
        }

        var codeIsValid =
            BCrypt.Net.BCrypt.Verify(
                normalizedCode,
                recoveryRequest.EmailCodeHash);

        if (!codeIsValid)
        {
            recoveryRequest.Attempts +=
                1;

            if (
                recoveryRequest.Attempts >=
                RecoveryMaxAttempts
            )
            {
                recoveryRequest.Status =
                    TwoFactorRecoveryRequestStatus
                        .Expired;
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                recoveryRequest.Status ==
                TwoFactorRecoveryRequestStatus.Expired
                    ? "Too many invalid attempts. Start recovery again."
                    : "Invalid email verification code.");
        }

        recoveryRequest.Status =
            TwoFactorRecoveryRequestStatus
                .PendingAdminReview;

        recoveryRequest.EmailVerifiedAt =
            now;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var submittedEmail =
            _emailTemplateService
                .GetTwoFactorRecoverySubmittedEmail();

        await _notificationQueue.QueueEmailAsync(
            recoveryRequest.User.Email,
            "Otrade Two-Factor Recovery Submitted",
            submittedEmail);

        var adminBody =
            _emailTemplateService
                .GetTwoFactorRecoveryAdminNotification(
                    recoveryRequest.User.Email,
                    recoveryRequest.User.ReferralCode,
                    $"{recoveryRequest.User.FirstName} " +
                    $"{recoveryRequest.User.LastName}",
                    recoveryRequest.User.KycStatus.ToString(),
                    recoveryRequest.UserDescription);

        await _notificationQueue.QueueAdminAsync(
            "Two-Factor Recovery Requires Review",
            adminBody);

        return ResponseFactory.Success(
            BuildRecoveryStatusResponse(
                recoveryRequest),
            "Email verified. Your recovery request is waiting for admin review.");
    }

    public async Task<ApiResponse<TwoFactorRecoveryStatusResponse>>
        GetRecoveryStatusAsync(
            TwoFactorRecoveryTokenRequest request)
    {
        if (
            request == null ||
            string.IsNullOrWhiteSpace(
                request.RecoveryToken)
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Invalid recovery request.");
        }

        var tokenHash =
            HashPublicToken(
                request.RecoveryToken.Trim());

        var recoveryRequest =
            await _context.TwoFactorRecoveryRequests
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.PublicTokenHash ==
                    tokenHash);

        if (recoveryRequest == null)
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "The recovery request is invalid or expired.");
        }

        var now =
            DateTime.UtcNow;

        if (
            recoveryRequest.ExpiresAt <= now &&
            recoveryRequest.Status is
                TwoFactorRecoveryRequestStatus
                    .PendingEmailVerification or
                TwoFactorRecoveryRequestStatus
                    .PendingAdminReview
        )
        {
            recoveryRequest.Status =
                TwoFactorRecoveryRequestStatus
                    .Expired;

            await _context.SaveChangesAsync();
        }

        return ResponseFactory.Success(
            BuildRecoveryStatusResponse(
                recoveryRequest));
    }

    // =========================================================
    // Admin review
    // =========================================================

    public async Task<ApiResponse<List<AdminTwoFactorRecoveryListItem>>>
        GetPendingAdminReviewsAsync()
    {
        var now =
            DateTime.UtcNow;

        await _context.TwoFactorRecoveryRequests
            .Where(x =>
                x.Status ==
                TwoFactorRecoveryRequestStatus
                    .PendingAdminReview &&
                x.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        x => x.Status,
                        TwoFactorRecoveryRequestStatus
                            .Expired));

        var items =
            await _context.TwoFactorRecoveryRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                    TwoFactorRecoveryRequestStatus
                        .PendingAdminReview)
                .OrderBy(x =>
                    x.CreatedAt)
                .Select(x =>
                    new AdminTwoFactorRecoveryListItem
                    {
                        RequestId =
                            x.TwoFactorRecoveryRequestId,

                        UserId =
                            x.UserId,

                        Uid =
                            x.User.ReferralCode,

                        FullName =
                            x.User.FirstName +
                            " " +
                            x.User.LastName,

                        Email =
                            x.User.Email,

                        KycStatus =
                            x.User.KycStatus
                                .ToString(),

                        Description =
                            x.UserDescription,

                        CreatedAt =
                            x.CreatedAt,

                        EmailVerifiedAt =
                            x.EmailVerifiedAt
                                ?? x.CreatedAt
                    })
                .ToListAsync();

        return ResponseFactory.Success(
            items);
    }

    public async Task<ApiResponse<TwoFactorRecoveryStatusResponse>>
        ApproveRecoveryAsync(
            long requestId,
            long adminUserId,
            ReviewTwoFactorRecoveryRequest request)
    {
        var note =
            request?.Note?.Trim()
            ?? string.Empty;

        if (
            note.Length < 10 ||
            note.Length > 1000
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Enter an admin review note using 10 to 1000 characters.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var recoveryRequest =
            await _context.TwoFactorRecoveryRequests
                .Include(x => x.User)
                .ThenInclude(x => x.RecoveryCodes)
                .FirstOrDefaultAsync(x =>
                    x.TwoFactorRecoveryRequestId ==
                    requestId);

        if (recoveryRequest == null)
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Recovery request not found.");
        }

        if (
            recoveryRequest.Status !=
            TwoFactorRecoveryRequestStatus
                .PendingAdminReview
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "This recovery request has already been reviewed.");
        }

        var now =
            DateTime.UtcNow;

        if (recoveryRequest.ExpiresAt <= now)
        {
            recoveryRequest.Status =
                TwoFactorRecoveryRequestStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "The recovery request has expired.");
        }

        var user =
            recoveryRequest.User;

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
        user.LastWithdrawalTotpStep =
            null;
        user.PendingTotpSecretEncrypted =
            null;

        user.PendingTotpCreatedAt =
            null;

        user.TotpRecoveryLockedUntil =
            now.AddHours(
                WithdrawalLockHoursAfterRecovery);

        user.MustChangePassword =
            true;

        user.AuthTokenVersion =
            Math.Max(
                1,
                user.AuthTokenVersion + 1);

        user.UpdatedAt =
            DateTime.Now;

        if (user.RecoveryCodes.Count > 0)
        {
            _context.UserRecoveryCodes
                .RemoveRange(
                    user.RecoveryCodes);
        }

        await _context.TwoFactorLoginChallenges
            .Where(x =>
                x.UserId == user.UserId &&
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

        await _context.TwoFactorRecoveryRequests
            .Where(x =>
                x.UserId == user.UserId &&
                x.TwoFactorRecoveryRequestId !=
                recoveryRequest
                    .TwoFactorRecoveryRequestId &&
                (
                    x.Status ==
                    TwoFactorRecoveryRequestStatus
                        .PendingEmailVerification ||
                    x.Status ==
                    TwoFactorRecoveryRequestStatus
                        .PendingAdminReview
                ))
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        x => x.Status,
                        TwoFactorRecoveryRequestStatus
                            .Canceled));

        recoveryRequest.Status =
            TwoFactorRecoveryRequestStatus
                .Approved;

        recoveryRequest.AdminNote =
            note;

        recoveryRequest.ReviewedByAdminId =
            adminUserId;

        recoveryRequest.ReviewedAt =
            now;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var approvedEmail =
            _emailTemplateService
                .GetTwoFactorRecoveryApprovedEmail(
                    user.TotpRecoveryLockedUntil
                        .Value);

        await _notificationQueue.QueueEmailAsync(
            user.Email,
            "Otrade Two-Factor Recovery Approved",
            approvedEmail);

        return ResponseFactory.Success(
            BuildRecoveryStatusResponse(
                recoveryRequest),
            "Two-factor recovery approved.");
    }

    public async Task<ApiResponse<TwoFactorRecoveryStatusResponse>>
        RejectRecoveryAsync(
            long requestId,
            long adminUserId,
            ReviewTwoFactorRecoveryRequest request)
    {
        var note =
            request?.Note?.Trim()
            ?? string.Empty;

        if (
            note.Length < 10 ||
            note.Length > 1000
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Enter a rejection reason using 10 to 1000 characters.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var recoveryRequest =
            await _context.TwoFactorRecoveryRequests
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TwoFactorRecoveryRequestId ==
                    requestId);

        if (recoveryRequest == null)
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "Recovery request not found.");
        }

        if (
            recoveryRequest.Status !=
            TwoFactorRecoveryRequestStatus
                .PendingAdminReview
        )
        {
            return ResponseFactory.Fail<TwoFactorRecoveryStatusResponse>(
                "This recovery request has already been reviewed.");
        }

        var now =
            DateTime.UtcNow;

        recoveryRequest.Status =
            TwoFactorRecoveryRequestStatus
                .Rejected;

        recoveryRequest.AdminNote =
            note;

        recoveryRequest.ReviewedByAdminId =
            adminUserId;

        recoveryRequest.ReviewedAt =
            now;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var rejectedEmail =
            _emailTemplateService
                .GetTwoFactorRecoveryRejectedEmail(
                    note);

        await _notificationQueue.QueueEmailAsync(
            recoveryRequest.User.Email,
            "Otrade Two-Factor Recovery Rejected",
            rejectedEmail);

        return ResponseFactory.Success(
            BuildRecoveryStatusResponse(
                recoveryRequest),
            "Two-factor recovery rejected.");
    }

    // =========================================================
    // Private helpers
    // =========================================================

    private async Task<bool> VerifyCurrentFactorAsync(
        User user,
        string suppliedCode)
    {
        var normalizedTotpCode =
            NormalizeTotpCode(
                suppliedCode);

        if (IsValidTotpCode(
                normalizedTotpCode))
        {
            var verification =
                await _twoFactorService
                    .VerifyTotpAsync(
                        user.UserId,
                        normalizedTotpCode,
                        preventReplay: true);

            return verification.Success;
        }

        var normalizedRecoveryCode =
            NormalizeRecoveryCode(
                suppliedCode);

        if (
            normalizedRecoveryCode.Length !=
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
            if (!BCrypt.Net.BCrypt.Verify(
                    normalizedRecoveryCode,
                    recoveryCode.CodeHash))
            {
                continue;
            }

            var usedAt =
                DateTime.UtcNow;

            var updatedRows =
                await _context.UserRecoveryCodes
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

    private TwoFactorSetupResponse BuildSetupResponse(
        string email,
        string secretKey,
        DateTime expiresAt)
    {
        var accountName =
            email.Trim();

        var otpAuthUri =
            BuildOtpAuthUri(
                accountName,
                secretKey);

        return new TwoFactorSetupResponse
        {
            QrCodeDataUrl =
                BuildQrCodeDataUrl(
                    otpAuthUri),

            ManualEntryKey =
                FormatManualEntryKey(
                    secretKey),

            Issuer =
                _issuer,

            AccountName =
                accountName,

            ExpiresAt =
                expiresAt
        };
    }

    private string BuildOtpAuthUri(
        string accountName,
        string secretKey)
    {
        var label =
            Uri.EscapeDataString(
                $"{_issuer}:{accountName}");

        return
            $"otpauth://totp/{label}" +
            $"?secret={Uri.EscapeDataString(secretKey)}" +
            $"&issuer={Uri.EscapeDataString(_issuer)}" +
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

        return
            "data:image/svg+xml;base64," +
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    svg));
    }

    private static string GenerateBase32Secret()
    {
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

        try
        {
            return Base32Encoding.ToString(
                secretBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                secretBytes);
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
                DateTime.UtcNow,
                normalizedCode,
                out matchedStep,
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

        var result =
            new List<string>();

        var uniqueCodes =
            new HashSet<string>(
                StringComparer.Ordinal);

        while (
            result.Count <
            RecoveryCodeCount
        )
        {
            var normalizedCode =
                GenerateRecoveryCode();

            if (!uniqueCodes.Add(
                    normalizedCode))
            {
                continue;
            }

            _context.UserRecoveryCodes.Add(
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
                });

            result.Add(
                FormatRecoveryCode(
                    normalizedCode));
        }

        return result;
    }

    private static string GenerateRecoveryCode()
    {
        var characters =
            new char[
                RecoveryCodeLength
            ];

        for (
            var index = 0;
            index < characters.Length;
            index++
        )
        {
            characters[index] =
                RecoveryAlphabet[
                    RandomNumberGenerator
                        .GetInt32(
                            RecoveryAlphabet.Length)
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

    private static string GenerateUrlSafeToken(
        int byteLength)
    {
        var bytes =
            new byte[byteLength];

        using (
            var random =
                RandomNumberGenerator.Create()
        )
        {
            random.GetBytes(
                bytes);
        }

        try
        {
            return Convert
                .ToBase64String(
                    bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                bytes);
        }
    }

    private static string HashPublicToken(
        string value)
    {
        var valueBytes =
            Encoding.UTF8.GetBytes(
                value);

        try
        {
            using var sha256 =
                SHA256.Create();

            return Convert
                .ToHexString(
                    sha256.ComputeHash(
                        valueBytes))
                .ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                valueBytes);
        }
    }

    private static string NormalizeTotpCode(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(
                code))
        {
            return string.Empty;
        }

        return new string(
            code
                .Where(char.IsDigit)
                .ToArray());
    }

    private static bool IsValidTotpCode(
        string code)
    {
        return
            code.Length ==
            TotpDigits &&
            code.All(char.IsDigit);
    }

    private static string NormalizeRecoveryCode(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(
                code))
        {
            return string.Empty;
        }

        return new string(
            code
                .Where(char.IsLetterOrDigit)
                .Select(
                    char.ToUpperInvariant)
                .ToArray());
    }

    private static string MaskEmail(
        string email)
    {
        if (string.IsNullOrWhiteSpace(
                email))
        {
            return "***";
        }

        var parts =
            email.Split('@');

        if (parts.Length != 2)
        {
            return "***";
        }

        var localPart =
            parts[0];

        var maskedLocal =
            localPart.Length switch
            {
                <= 1 =>
                    "*",

                2 =>
                    $"{localPart[0]}*",

                _ =>
                    $"{localPart[0]}" +
                    new string(
                        '*',
                        Math.Min(
                            6,
                            localPart.Length - 2)) +
                    $"{localPart[^1]}"
            };

        return
            $"{maskedLocal}@{parts[1]}";
    }

    private static TwoFactorRecoveryStatusResponse
        BuildRecoveryStatusResponse(
            TwoFactorRecoveryRequest request)
    {
        var message =
            request.Status switch
            {
                TwoFactorRecoveryRequestStatus
                    .PendingEmailVerification =>
                    "Email verification is required.",

                TwoFactorRecoveryRequestStatus
                    .PendingAdminReview =>
                    "The request is waiting for admin review.",

                TwoFactorRecoveryRequestStatus
                    .Approved =>
                    "The recovery request was approved. " +
                    "Login again and change your password.",

                TwoFactorRecoveryRequestStatus
                    .Rejected =>
                    "The recovery request was rejected.",

                TwoFactorRecoveryRequestStatus
                    .Canceled =>
                    "The recovery request was canceled.",

                _ =>
                    "The recovery request expired."
            };

        return new TwoFactorRecoveryStatusResponse
        {
            Status =
                request.Status.ToString(),

            Message =
                message,

            CreatedAt =
                request.CreatedAt,

            ExpiresAt =
                request.ExpiresAt,

            ReviewedAt =
                request.ReviewedAt,

            WithdrawalLockedUntil =
                request.Status ==
                TwoFactorRecoveryRequestStatus
                    .Approved
                    ? request.User
                        .TotpRecoveryLockedUntil
                    : null
        };
    }
}
