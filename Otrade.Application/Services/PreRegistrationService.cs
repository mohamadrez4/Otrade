using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Auth;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System.ComponentModel.DataAnnotations;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.Services.Security;
using System.Security.Cryptography;
namespace Otrade.Application.Services;

public class PreRegistrationService
{
    private readonly OtradeDbContext _context;
    private readonly SystemSettingService _settingService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationQueue _notificationQueue;
    private readonly JwtService _jwtService;
    public PreRegistrationService(
        OtradeDbContext context,
        SystemSettingService settingService,
        IEmailTemplateService emailTemplateService,
        INotificationQueue notificationQueue,
        JwtService jwtService)
    {
        _context = context;
        _settingService = settingService;
        _emailTemplateService = emailTemplateService;
        _notificationQueue = notificationQueue;
        _jwtService = jwtService;
    }

    public async Task<ApiResponse<StartPreRegistrationResponse>> StartAsync(
        StartPreRegistrationRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Invalid request");

        var email = request.Email?.Trim().ToLowerInvariant();
        var referralCode = request.ReferralCode?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter email");

        if (!new EmailAddressAttribute().IsValid(email))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter a valid email address");

        if (string.IsNullOrWhiteSpace(referralCode))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter referral code");

        var emailExists = await _context.Users
            .AnyAsync(x => x.Email == email);

        if (emailExists)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Email already exists");

        var sponsor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReferralCode == referralCode);

        if (sponsor == null)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Referral code is invalid");

        var now = DateTime.Now;

        var activeTemporaryRegistration = await _context.TemporaryRegistrations
            .Where(x =>
                x.Email == email &&
                x.ExpiresAt > now &&
                x.Status != TemporaryRegistrationStatus.Completed &&
                x.Status != TemporaryRegistrationStatus.Rejected &&
                x.Status != TemporaryRegistrationStatus.Expired)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeTemporaryRegistration != null)
        {
            if (activeTemporaryRegistration.EmailVerifiedAt == null &&
                activeTemporaryRegistration.Status == TemporaryRegistrationStatus.EmailRegistered)
            {
                var resendCode = GenerateVerificationCode();

                activeTemporaryRegistration.EmailVerificationCode = resendCode;
                activeTemporaryRegistration.EmailVerificationExpireAt = now.AddMinutes(10);
                activeTemporaryRegistration.UpdatedAt = now;

                await _context.SaveChangesAsync();

                var resendBody = _emailTemplateService.GetVerificationEmail(resendCode);

                await _notificationQueue.QueueEmailAsync(
                    activeTemporaryRegistration.Email,
                    "Otrade Email Verification",
                    resendBody);
            }

            return ResponseFactory.Success(
                new StartPreRegistrationResponse
                {
                    TemporaryRegistrationId = activeTemporaryRegistration.Id,
                    Email = activeTemporaryRegistration.Email,
                    SponsorId = activeTemporaryRegistration.SponsorId,
                    Status = activeTemporaryRegistration.Status.ToString(),
                    IsEmailVerified = activeTemporaryRegistration.EmailVerifiedAt != null,
                    EmailVerificationExpireAt = activeTemporaryRegistration.EmailVerificationExpireAt,
                    ExpiresAt = activeTemporaryRegistration.ExpiresAt
                },
                activeTemporaryRegistration.EmailVerifiedAt == null
                    ? "Verification code sent"
                    : "Pre-registration already exists");
        }

        var expireHours = await _settingService.GetIntAsync("InitialRegistrationExpireHours") ?? 72;

        var verificationCode = GenerateVerificationCode();

        var temporaryRegistration = new TemporaryRegistration
        {
            Email = email,
            ReferralCode = referralCode,
            SponsorId = sponsor.UserId,
            Status = TemporaryRegistrationStatus.EmailRegistered,

            EmailVerificationCode = verificationCode,
            EmailVerificationExpireAt = now.AddMinutes(10),
            EmailVerifiedAt = null,

            ExpiresAt = now.AddHours(expireHours),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.TemporaryRegistrations.Add(temporaryRegistration);

        await _context.SaveChangesAsync();

        var emailBody = _emailTemplateService.GetVerificationEmail(verificationCode);

        await _notificationQueue.QueueEmailAsync(
            temporaryRegistration.Email,
            "Otrade Email Verification",
            emailBody);

        return ResponseFactory.Success(
            new StartPreRegistrationResponse
            {
                TemporaryRegistrationId = temporaryRegistration.Id,
                Email = temporaryRegistration.Email,
                SponsorId = temporaryRegistration.SponsorId,
                Status = temporaryRegistration.Status.ToString(),
                IsEmailVerified = false,
                EmailVerificationExpireAt = temporaryRegistration.EmailVerificationExpireAt,
                ExpiresAt = temporaryRegistration.ExpiresAt
            },
            "Verification code sent");
    }
    public async Task<ApiResponse<VerifyPreRegistrationEmailResponse>> VerifyEmailAsync(
    VerifyPreRegistrationEmailRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Invalid request");

        var email = request.Email?.Trim().ToLowerInvariant();
        var code = request.Code?.Trim();

        if (request.TemporaryRegistrationId <= 0)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Invalid pre-registration id");

        if (string.IsNullOrWhiteSpace(email))
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Email is required");

        if (string.IsNullOrWhiteSpace(code))
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Verification code is required");

        if (code.Length != 6)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Verification code is invalid");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x =>
                x.Id == request.TemporaryRegistrationId &&
                x.Email == email);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Pre-registration not found");

        if (temporaryRegistration.ExpiresAt <= now)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Pre-registration has expired");
        }

        if (temporaryRegistration.EmailVerifiedAt != null)
        {
            return ResponseFactory.Success(
                new VerifyPreRegistrationEmailResponse
                {
                    TemporaryRegistrationId = temporaryRegistration.Id,
                    Email = temporaryRegistration.Email,
                    IsEmailVerified = true,
                    Status = temporaryRegistration.Status.ToString()
                },
                "Email already verified");
        }

        if (string.IsNullOrWhiteSpace(temporaryRegistration.EmailVerificationCode))
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Verification code not found");

        if (temporaryRegistration.EmailVerificationExpireAt == null ||
            temporaryRegistration.EmailVerificationExpireAt < now)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Verification code expired");

        if (temporaryRegistration.EmailVerificationCode != code)
            return ResponseFactory.Fail<VerifyPreRegistrationEmailResponse>("Invalid verification code");

        temporaryRegistration.EmailVerifiedAt = now;
        temporaryRegistration.EmailVerificationCode = null;
        temporaryRegistration.EmailVerificationExpireAt = null;
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            new VerifyPreRegistrationEmailResponse
            {
                TemporaryRegistrationId = temporaryRegistration.Id,
                Email = temporaryRegistration.Email,
                IsEmailVerified = true,
                Status = temporaryRegistration.Status.ToString()
            },
            "Email verified successfully");
    }
    public async Task<ApiResponse<SubmitPreRegistrationDepositResponse>> SubmitDepositAsync(
        SubmitPreRegistrationDepositRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Invalid request");

        if (request.TemporaryRegistrationId <= 0)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Invalid pre-registration id");

        if (request.Amount <= 0)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Amount must be greater than zero");

        var txId = request.TxId?.Trim();

        if (string.IsNullOrWhiteSpace(txId))
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Transaction hash is required");

        if (txId.Length < 10)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Transaction hash is invalid");

        if (txId.Length > 200)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Transaction hash is too long");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x => x.Id == request.TemporaryRegistrationId);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Pre-registration not found");
        if (temporaryRegistration.EmailVerifiedAt == null)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Please verify your email before submitting deposit");
        if (temporaryRegistration.ExpiresAt <= now)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Pre-registration has expired");
        }

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Completed)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Pre-registration is already completed");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Rejected)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Pre-registration is rejected");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Approved)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Pre-registration is already approved");

        var txIdUsedInTemporaryRegistrations = await _context.TemporaryRegistrations
            .AnyAsync(x =>
                x.DepositTxId == txId &&
                x.Id != temporaryRegistration.Id);

        if (txIdUsedInTemporaryRegistrations)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Transaction hash is already used");

        var txIdUsedInDeposits = await _context.Deposits
            .AnyAsync(x => x.TxId == txId);

        if (txIdUsedInDeposits)
            return ResponseFactory.Fail<SubmitPreRegistrationDepositResponse>("Transaction hash is already used");

        var trackingToken = temporaryRegistration.TrackingToken;

        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            trackingToken = GenerateTrackingToken();
            temporaryRegistration.TrackingToken = trackingToken;
        }

        temporaryRegistration.DeclaredAmount = request.Amount;
        temporaryRegistration.DepositTxId = txId;
        temporaryRegistration.Status = TemporaryRegistrationStatus.DepositSubmitted;
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        var emailBody = _emailTemplateService.GetDepositNotification(
            temporaryRegistration.Email,
            request.Amount,
            txId);

        await _notificationQueue.QueueAdminAsync(
            "New Pre-Registration Deposit",
            emailBody);

        return ResponseFactory.Success(
            new SubmitPreRegistrationDepositResponse
            {
                TemporaryRegistrationId = temporaryRegistration.Id,
                Email = temporaryRegistration.Email,
                Amount = request.Amount,
                TxId = txId,
                Status = temporaryRegistration.Status.ToString(),
                TrackingToken = trackingToken,
                ExpiresAt = temporaryRegistration.ExpiresAt
            },
            "Deposit submitted successfully");
    }
    public async Task<ApiResponse<List<PreRegistrationPendingDto>>> GetPendingForAdminAsync()
    {
        var now = DateTime.Now;

        var expiredItems = await _context.TemporaryRegistrations
            .Where(x =>
                x.ExpiresAt <= now &&
                x.Status != TemporaryRegistrationStatus.Completed &&
                x.Status != TemporaryRegistrationStatus.Rejected &&
                x.Status != TemporaryRegistrationStatus.Expired)
            .ToListAsync();

        if (expiredItems.Any())
        {
            foreach (var item in expiredItems)
            {
                item.Status = TemporaryRegistrationStatus.Expired;
                item.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
        }

        var items = await _context.TemporaryRegistrations
            .AsNoTracking()
            .Include(x => x.Sponsor)
            .Where(x => x.Status == TemporaryRegistrationStatus.DepositSubmitted)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new PreRegistrationPendingDto
            {
                Id = x.Id,
                Email = x.Email,
                ReferralCode = x.ReferralCode,
                SponsorId = x.SponsorId,
                SponsorEmail = x.Sponsor != null ? x.Sponsor.Email : null,
                DeclaredAmount = x.DeclaredAmount,
                DepositTxId = x.DepositTxId,
                Status = x.Status.ToString(),
                ExpiresAt = x.ExpiresAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return ResponseFactory.Success(items);
    }
    public async Task<ApiResponse<ApprovePreRegistrationResponse>> ApproveAsync(
        long temporaryRegistrationId,
        decimal approvedAmount,
        long adminUserId)
    {
        if (temporaryRegistrationId <= 0)
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Invalid pre-registration id");

        if (approvedAmount <= 0)
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Approved amount must be greater than zero");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x => x.Id == temporaryRegistrationId);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Pre-registration not found");

        if (temporaryRegistration.ExpiresAt <= now)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Pre-registration has expired");
        }

        if (temporaryRegistration.Status != TemporaryRegistrationStatus.DepositSubmitted)
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Only submitted deposits can be approved");

        if (string.IsNullOrWhiteSpace(temporaryRegistration.DepositTxId))
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("Transaction hash is missing");

        var emailAlreadyExists = await _context.Users
            .AnyAsync(x => x.Email == temporaryRegistration.Email);

        if (emailAlreadyExists)
            return ResponseFactory.Fail<ApprovePreRegistrationResponse>("A user with this email already exists");

        var completionToken = GenerateCompletionToken();

        temporaryRegistration.ApprovedAmount = approvedAmount;
        temporaryRegistration.ApprovedByUserId = adminUserId;
        temporaryRegistration.ApprovedAt = now;
        temporaryRegistration.CompletionToken = completionToken;
        temporaryRegistration.Status = TemporaryRegistrationStatus.Approved;
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            new ApprovePreRegistrationResponse
            {
                TemporaryRegistrationId = temporaryRegistration.Id,
                Email = temporaryRegistration.Email,
                ApprovedAmount = approvedAmount,
                CompletionToken = completionToken,
                Status = temporaryRegistration.Status.ToString(),
                ApprovedAt = now
            },
            "Pre-registration approved");
    }
    public async Task<ApiResponse<bool>> RejectAsync(
        long temporaryRegistrationId,
        string reason)
    {
        if (temporaryRegistrationId <= 0)
            return ResponseFactory.Fail<bool>("Invalid pre-registration id");

        reason = reason?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(reason))
            return ResponseFactory.Fail<bool>("Reject reason is required");

        if (reason.Length > 500)
            return ResponseFactory.Fail<bool>("Reject reason is too long");

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x => x.Id == temporaryRegistrationId);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<bool>("Pre-registration not found");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Completed)
            return ResponseFactory.Fail<bool>("Completed pre-registration cannot be rejected");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Approved)
            return ResponseFactory.Fail<bool>("Approved pre-registration cannot be rejected here");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Rejected)
            return ResponseFactory.Fail<bool>("Pre-registration is already rejected");

        var now = DateTime.Now;

        temporaryRegistration.Status = TemporaryRegistrationStatus.Rejected;
        temporaryRegistration.RejectReason = reason;
        temporaryRegistration.UpdatedAt = now;
        var emailBody = _emailTemplateService.GetDepositRejectedEmail(
    temporaryRegistration.DeclaredAmount,
    reason);

        await _notificationQueue.QueueEmailAsync(
            temporaryRegistration.Email,
            "Deposit Status",
            emailBody);
        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "Pre-registration rejected");
    }

    public async Task<ApiResponse<CompletePreRegistrationResponse>> CompleteAsync(
    CompletePreRegistrationRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Invalid request");

        var completionToken = request.CompletionToken?.Trim();

        if (string.IsNullOrWhiteSpace(completionToken))
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Completion token is required");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Please enter firstname");

        if (string.IsNullOrWhiteSpace(request.LastName))
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Please enter lastname");

        if (string.IsNullOrWhiteSpace(request.Password))
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Please enter password");

        if (request.Password.Length < 6)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Password must be at least 6 characters");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x => x.CompletionToken == completionToken);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Invalid completion token");

        if (temporaryRegistration.Status != TemporaryRegistrationStatus.Approved)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Pre-registration is not approved");

        if (temporaryRegistration.ExpiresAt <= now)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Pre-registration has expired");
        }

        if (temporaryRegistration.ApprovedAmount == null || temporaryRegistration.ApprovedAmount <= 0)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Approved amount is invalid");

        if (string.IsNullOrWhiteSpace(temporaryRegistration.DepositTxId))
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Transaction hash is missing");

        var emailExists = await _context.Users
            .AnyAsync(x => x.Email == temporaryRegistration.Email);

        if (emailExists)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Email already exists");

        var depositTxExists = await _context.Deposits
            .AnyAsync(x => x.TxId == temporaryRegistration.DepositTxId);

        if (depositTxExists)
            return ResponseFactory.Fail<CompletePreRegistrationResponse>("Transaction hash is already used");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var referralCode = await GenerateCode();

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = temporaryRegistration.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            ReferralCode = referralCode,
            SponsorId = temporaryRegistration.SponsorId,
            EmailConfirmed = true,
            IsOwner = false,
            IsAdmin = false,
            KycStatus = KycStatus.Pending,
            CurrentRankId = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        var approvedAmount = temporaryRegistration.ApprovedAmount.Value;

        var mainWallet = new Wallet
        {
            UserId = user.UserId,
            WalletType = WalletType.Main,
            Balance = approvedAmount,
            IsLocked = false,
            CreatedAt = now
        };

        var investWallet = new Wallet
        {
            UserId = user.UserId,
            WalletType = WalletType.Invest,
            Balance = 0,
            IsLocked = false,
            CreatedAt = now
        };

        var profitWallet = new Wallet
        {
            UserId = user.UserId,
            WalletType = WalletType.Profit,
            Balance = 0,
            IsLocked = false,
            CreatedAt = now
        };

        var referralWallet = new Wallet
        {
            UserId = user.UserId,
            WalletType = WalletType.Referral,
            Balance = 0,
            IsLocked = false,
            CreatedAt = now
        };

        _context.Wallets.AddRange(
            mainWallet,
            investWallet,
            profitWallet,
            referralWallet);

        await _context.SaveChangesAsync();

        _context.Deposits.Add(new Deposit
        {
            UserId = user.UserId,
            Amount = approvedAmount,
            TxId = temporaryRegistration.DepositTxId,
            Status = DepositStatus.Approved,
            CreatedAt = now
        });

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = user.UserId,
            WalletId = mainWallet.WalletId,
            Amount = approvedAmount,
            BalanceBefore = 0,
            BalanceAfter = approvedAmount,
            Type = TransactionType.Deposit,
            Description = $"Pre-registration deposit approved and credited to Main Wallet (TxId: {temporaryRegistration.DepositTxId})",
            CreatedAt = now
        });

        await AddReferralRelationsAsync(user.UserId, user.SponsorId);

        temporaryRegistration.CompletedUserId = user.UserId;
        temporaryRegistration.CompletedAt = now;
        temporaryRegistration.Status = TemporaryRegistrationStatus.Completed;
        temporaryRegistration.CompletionToken = null;
        temporaryRegistration.TrackingToken = null;
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var jwtToken = _jwtService.GenerateToken(
            user.UserId,
            user.Email,
            user.IsAdmin,
            user.IsOwner);

        return ResponseFactory.Success(
            new CompletePreRegistrationResponse
            {
                UserId = user.UserId,
                TemporaryRegistrationId = temporaryRegistration.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ReferralCode = user.ReferralCode,
                MainWalletBalance = approvedAmount,
                InvestWalletBalance = 0,
                ApprovedAmount = approvedAmount,
                Token = jwtToken
            },
            "Registration completed successfully");
    }
    public async Task<ApiResponse<PreRegistrationWaitStatusResponse>> GetWaitStatusAsync(
    PreRegistrationWaitStatusRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<PreRegistrationWaitStatusResponse>("Invalid request");

        var trackingToken = request.TrackingToken?.Trim();

        if (string.IsNullOrWhiteSpace(trackingToken))
            return ResponseFactory.Fail<PreRegistrationWaitStatusResponse>("Tracking token is required");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x => x.TrackingToken == trackingToken);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<PreRegistrationWaitStatusResponse>("Request not found");

        if (temporaryRegistration.ExpiresAt <= now &&
            temporaryRegistration.Status != TemporaryRegistrationStatus.Completed &&
            temporaryRegistration.Status != TemporaryRegistrationStatus.Rejected &&
            temporaryRegistration.Status != TemporaryRegistrationStatus.Expired)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }

        var waitMinutes = await _settingService.GetIntAsync("PreRegistrationWaitMinutes") ?? 15;

        var waitEndsAt = temporaryRegistration.UpdatedAt.AddMinutes(waitMinutes);

        var remainingSeconds = waitEndsAt > now
            ? (int)(waitEndsAt - now).TotalSeconds
            : 0;

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Approved)
        {
            if (string.IsNullOrWhiteSpace(temporaryRegistration.CompletionToken))
            {
                temporaryRegistration.CompletionToken = GenerateCompletionToken();
                temporaryRegistration.UpdatedAt = now;

                await _context.SaveChangesAsync();
            }

            return ResponseFactory.Success(new PreRegistrationWaitStatusResponse
            {
                Status = temporaryRegistration.Status.ToString(),
                IsPending = false,
                IsApproved = true,
                IsRejected = false,
                IsExpired = false,
                IsWaitOver = false,
                CompletionToken = temporaryRegistration.CompletionToken,
                ApprovedAmount = temporaryRegistration.ApprovedAmount,
                RemainingSeconds = 0,
                Message = "Deposit approved. You can complete your registration."
            });
        }

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Rejected)
        {
            return ResponseFactory.Success(new PreRegistrationWaitStatusResponse
            {
                Status = temporaryRegistration.Status.ToString(),
                IsPending = false,
                IsApproved = false,
                IsRejected = true,
                IsExpired = false,
                IsWaitOver = false,
                RejectReason = temporaryRegistration.RejectReason,
                RemainingSeconds = 0,
                Message = "Deposit rejected."
            });
        }

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Expired)
        {
            return ResponseFactory.Success(new PreRegistrationWaitStatusResponse
            {
                Status = temporaryRegistration.Status.ToString(),
                IsPending = false,
                IsApproved = false,
                IsRejected = false,
                IsExpired = true,
                IsWaitOver = true,
                RemainingSeconds = 0,
                Message = "Pre-registration has expired."
            });
        }

        var isWaitOver = remainingSeconds <= 0;

        return ResponseFactory.Success(new PreRegistrationWaitStatusResponse
        {
            Status = temporaryRegistration.Status.ToString(),
            IsPending = true,
            IsApproved = false,
            IsRejected = false,
            IsExpired = false,
            IsWaitOver = isWaitOver,
            RemainingSeconds = remainingSeconds,
            Message = isWaitOver
                ? "Waiting time is over, but request is still pending."
                : "Waiting for admin approval."
        });
    }
    public async Task<ApiResponse<RecoverPreRegistrationResponse>> RecoverAsync(
        RecoverPreRegistrationRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Invalid request");

        var email = request.Email?.Trim().ToLowerInvariant();
        var txId = request.TxId?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Email is required");

        if (!new EmailAddressAttribute().IsValid(email))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Invalid email");

        if (string.IsNullOrWhiteSpace(txId))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Transaction hash is required");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x =>
                x.Email == email &&
                x.DepositTxId == txId);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Request not found");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Completed)
        {
            return ResponseFactory.Success(new RecoverPreRegistrationResponse
            {
                TrackingToken = string.Empty,
                Status = temporaryRegistration.Status.ToString(),
                CanContinueWaiting = false,
                IsApproved = false,
                IsRejected = false,
                IsExpired = false,
                Message = "Registration is already completed."
            });
        }

        if (temporaryRegistration.ExpiresAt <= now &&
            temporaryRegistration.Status != TemporaryRegistrationStatus.Rejected &&
            temporaryRegistration.Status != TemporaryRegistrationStatus.Expired)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Rejected)
        {
            return ResponseFactory.Success(new RecoverPreRegistrationResponse
            {
                TrackingToken = string.Empty,
                Status = temporaryRegistration.Status.ToString(),
                CanContinueWaiting = false,
                IsApproved = false,
                IsRejected = true,
                IsExpired = false,
                Message = "This pre-registration was rejected."
            });
        }

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Expired)
        {
            return ResponseFactory.Success(new RecoverPreRegistrationResponse
            {
                TrackingToken = string.Empty,
                Status = temporaryRegistration.Status.ToString(),
                CanContinueWaiting = false,
                IsApproved = false,
                IsRejected = false,
                IsExpired = true,
                Message = "This pre-registration has expired."
            });
        }

        var recoveryCode = GenerateVerificationCode();

        temporaryRegistration.RecoveryVerificationCode = recoveryCode;
        temporaryRegistration.RecoveryVerificationExpireAt = now.AddMinutes(10);
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        var emailBody = _emailTemplateService.GetVerificationEmail(recoveryCode);

        await _notificationQueue.QueueEmailAsync(
            temporaryRegistration.Email,
            "Otrade Recovery Verification",
            emailBody);

        return ResponseFactory.Success(new RecoverPreRegistrationResponse
        {
            TrackingToken = string.Empty,
            Status = temporaryRegistration.Status.ToString(),
            CanContinueWaiting = true,
            IsApproved = temporaryRegistration.Status == TemporaryRegistrationStatus.Approved,
            IsRejected = false,
            IsExpired = false,
            Message = "Recovery verification code sent to your email."
        });
    }
    public async Task<ApiResponse<RecoverPreRegistrationResponse>> VerifyRecoveryAsync(
    VerifyRecoverPreRegistrationRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Invalid request");

        var email = request.Email?.Trim().ToLowerInvariant();
        var txId = request.TxId?.Trim();
        var code = request.Code?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Email is required");

        if (!new EmailAddressAttribute().IsValid(email))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Invalid email");

        if (string.IsNullOrWhiteSpace(txId))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Transaction hash is required");

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Verification code is invalid");

        var now = DateTime.Now;

        var temporaryRegistration = await _context.TemporaryRegistrations
            .FirstOrDefaultAsync(x =>
                x.Email == email &&
                x.DepositTxId == txId);

        if (temporaryRegistration == null)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Request not found");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Completed)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Registration is already completed");

        if (temporaryRegistration.Status == TemporaryRegistrationStatus.Rejected)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("This pre-registration was rejected");

        if (temporaryRegistration.ExpiresAt <= now)
        {
            temporaryRegistration.Status = TemporaryRegistrationStatus.Expired;
            temporaryRegistration.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("This pre-registration has expired");
        }

        if (string.IsNullOrWhiteSpace(temporaryRegistration.RecoveryVerificationCode))
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Recovery code not found");

        if (temporaryRegistration.RecoveryVerificationExpireAt == null ||
            temporaryRegistration.RecoveryVerificationExpireAt < now)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Recovery code expired");

        if (temporaryRegistration.RecoveryVerificationCode != code)
            return ResponseFactory.Fail<RecoverPreRegistrationResponse>("Invalid recovery code");

        if (string.IsNullOrWhiteSpace(temporaryRegistration.TrackingToken))
        {
            temporaryRegistration.TrackingToken = GenerateTrackingToken();
        }

        temporaryRegistration.RecoveryVerificationCode = null;
        temporaryRegistration.RecoveryVerificationExpireAt = null;
        temporaryRegistration.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(new RecoverPreRegistrationResponse
        {
            TrackingToken = temporaryRegistration.TrackingToken,
            Status = temporaryRegistration.Status.ToString(),
            CanContinueWaiting =
                temporaryRegistration.Status == TemporaryRegistrationStatus.DepositSubmitted ||
                temporaryRegistration.Status == TemporaryRegistrationStatus.Approved,
            IsApproved = temporaryRegistration.Status == TemporaryRegistrationStatus.Approved,
            IsRejected = false,
            IsExpired = false,
            Message = "Recovery verified successfully."
        });
    }
    public async Task<ApiResponse<PreRegistrationDepositInfoResponse>> GetDepositInfoAsync()
    {
        var walletAddress = await _settingService.GetValueAsync("SiteWalletAddress");
        var network = await _settingService.GetValueAsync("Network");

        return ResponseFactory.Success(
            new PreRegistrationDepositInfoResponse
            {
                WalletAddress = walletAddress ?? string.Empty,
                Network = network ?? "USDT"
            });
    }
    private async Task<string> GenerateCode()
    {
        var gencode = "OTR" + GenerateVerificationCode();
        var user = await _context.Users.AnyAsync(x => x.ReferralCode == gencode);
        if (!user)
            return gencode;
        return await GenerateCode();
    }
    private string GenerateVerificationCode()
    {
        var code = RandomNumberGenerator
        .GetInt32(100000, 999999)
        .ToString();

        return code;
    }
    private async Task AddReferralRelationsAsync(long newUserId, long? sponsorId)
    {
        if (sponsorId == null)
            return;

        var directExists = await _context.ReferralRelations
            .AnyAsync(x =>
                x.AncestorId == sponsorId.Value &&
                x.DescendantId == newUserId);

        if (!directExists)
        {
            _context.ReferralRelations.Add(new ReferralRelation
            {
                AncestorId = sponsorId.Value,
                DescendantId = newUserId,
                Depth = 1
            });
        }

        var ancestors = await _context.ReferralRelations
            .Where(x => x.DescendantId == sponsorId.Value)
            .Select(x => new
            {
                x.AncestorId,
                x.Depth
            })
            .ToListAsync();

        foreach (var ancestor in ancestors)
        {
            var relationExists = await _context.ReferralRelations
                .AnyAsync(x =>
                    x.AncestorId == ancestor.AncestorId &&
                    x.DescendantId == newUserId);

            if (relationExists)
                continue;

            _context.ReferralRelations.Add(new ReferralRelation
            {
                AncestorId = ancestor.AncestorId,
                DescendantId = newUserId,
                Depth = ancestor.Depth + 1
            });
        }
    }
    private static string GenerateCompletionToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
    }
    private static string GenerateTrackingToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
    }
}