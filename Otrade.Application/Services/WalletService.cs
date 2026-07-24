using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Common;
using Otrade.Application.DTOs.Wallet;
using Otrade.Application.Services.Security;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System;
using System.Data;
using System.Security.Cryptography;

namespace Otrade.Application.Services;

public class WalletService
{
    private readonly OtradeDbContext _context;
    private readonly MainInvestBonusService _mainInvestBonusService;
    private readonly SystemSettingService _settingService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationQueue _notificationQueue;
    private readonly InvestmentCapacityService _investmentCapacityService;
    private readonly PaymentTransactionGuardService _paymentTransactionGuardService;
    private readonly TwoFactorAuthenticationService _twoFactorAuthenticationService;
    public WalletService(
     OtradeDbContext context,
     MainInvestBonusService mainInvestBonusService,
     InvestmentCapacityService investmentCapacityService,
     SystemSettingService systemSettingService,
     IEmailService emailService,
     IEmailTemplateService emailTemplateService,
     INotificationQueue notificationQueue,
     PaymentTransactionGuardService paymentTransactionGuardService,
     TwoFactorAuthenticationService twoFactorAuthenticationService)
    {
        _context = context;
        _mainInvestBonusService = mainInvestBonusService;
        _investmentCapacityService = investmentCapacityService;
        _settingService = systemSettingService;
        _emailTemplateService = emailTemplateService;
        _notificationQueue = notificationQueue;
        _paymentTransactionGuardService = paymentTransactionGuardService;
        _twoFactorAuthenticationService = twoFactorAuthenticationService;
    }
    public async Task<ApiResponse<InternalTransferRecipientDto>> FindInternalTransferRecipientAsync(
    string query,
    long senderUserId)
    {
        var searchText = query?.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
            return ResponseFactory.Fail<InternalTransferRecipientDto>("Receiver UID or email is required");

        var receiver = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.ReferralCode == searchText ||
                x.Email == searchText)
            .Select(x => new
            {
                x.UserId,
                x.ReferralCode,
                x.Email,
                x.FirstName,
                x.LastName
            })
            .FirstOrDefaultAsync();

        if (receiver == null)
            return ResponseFactory.Fail<InternalTransferRecipientDto>("Receiver not found");

        if (receiver.UserId == senderUserId)
            return ResponseFactory.Fail<InternalTransferRecipientDto>("You cannot transfer to yourself");

        var fullName = $"{receiver.FirstName} {receiver.LastName}".Trim();

        return ResponseFactory.Success(new InternalTransferRecipientDto
        {
            UserId = receiver.UserId,
            Uid = receiver.ReferralCode,
            Email = receiver.Email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "-" : fullName
        });
    }
    public async Task<ApiResponse<CreateInternalTransferVerificationResponse>> CreateInternalTransferVerificationAsync(
    CreateInternalTransferVerificationRequest request,
    long senderUserId)
    {
        var receiverSearch = request.Receiver?.Trim();

        if (string.IsNullOrWhiteSpace(receiverSearch))
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Receiver UID or email is required");

        if (request.Amount <= 0)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Invalid amount");

        var sender = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == senderUserId);

        if (sender == null)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Sender not found");

        var receiver = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ReferralCode == receiverSearch ||
                x.Email == receiverSearch);

        if (receiver == null)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Receiver not found");

        if (receiver.UserId == senderUserId)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("You cannot transfer to yourself");

        var senderMainWallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserId == senderUserId &&
                x.WalletType == WalletType.Main);

        if (senderMainWallet == null)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Sender Main Wallet not found");

        if (senderMainWallet.IsLocked)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Sender Main Wallet is locked");

        if (senderMainWallet.Balance < request.Amount)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Insufficient Main Wallet balance");

        var receiverMainWalletExists = await _context.Wallets
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == receiver.UserId &&
                x.WalletType == WalletType.Main);

        if (!receiverMainWalletExists)
            return ResponseFactory.Fail<CreateInternalTransferVerificationResponse>("Receiver Main Wallet not found");

        var now = DateTime.Now;
        var expiresInMinutes = 10;

        var activeVerifications = await _context.InternalTransferVerifications
            .Where(x =>
                x.SenderUserId == senderUserId &&
                x.Status == InternalTransferVerificationStatus.Pending)
            .ToListAsync();

        foreach (var item in activeVerifications)
        {
            item.Status = InternalTransferVerificationStatus.Expired;
        }

        var code = GenerateInternalTransferCode();

        var verification = new InternalTransferVerification
        {
            SenderUserId = senderUserId,
            ReceiverUserId = receiver.UserId,
            Amount = request.Amount,
            Description = request.Description?.Trim(),
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            Status = InternalTransferVerificationStatus.Pending,
            Attempts = 0,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(expiresInMinutes)
        };

        _context.InternalTransferVerifications.Add(verification);

        await _context.SaveChangesAsync();

        var receiverDisplay = BuildUserDisplay(receiver.FirstName, receiver.LastName, receiver.Email, receiver.ReferralCode);

        var emailBody = _emailTemplateService.GetInternalTransferVerificationEmail(
            request.Amount,
            receiverDisplay,
            code,
            expiresInMinutes);

        await _notificationQueue.QueueEmailAsync(
            sender.Email,
            "Internal Transfer Verification Code",
            emailBody);

        return ResponseFactory.Success(new CreateInternalTransferVerificationResponse
        {
            VerificationId = verification.InternalTransferVerificationId,
            ReceiverUid = receiver.ReferralCode,
            ReceiverEmail = receiver.Email,
            ReceiverFullName = $"{receiver.FirstName} {receiver.LastName}".Trim(),
            Amount = request.Amount,
            ExpiresInMinutes = expiresInMinutes
        }, "Verification code sent to your email");
    }
    public async Task<ApiResponse<bool>> ConfirmInternalTransferAsync(
    ConfirmInternalTransferRequest request,
    long senderUserId)
    {
        var code = request.Code?.Trim();

        if (request.VerificationId <= 0)
            return ResponseFactory.Fail<bool>("Verification is required");

        if (string.IsNullOrWhiteSpace(code))
            return ResponseFactory.Fail<bool>("Verification code is required");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var verification = await _context.InternalTransferVerifications
            .Include(x => x.SenderUser)
            .Include(x => x.ReceiverUser)
            .FirstOrDefaultAsync(x =>
                x.InternalTransferVerificationId == request.VerificationId &&
                x.SenderUserId == senderUserId);

        if (verification == null)
            return ResponseFactory.Fail<bool>("Verification request not found");

        if (verification.Status != InternalTransferVerificationStatus.Pending)
            return ResponseFactory.Fail<bool>("Verification request is not pending");

        var now = DateTime.Now;

        if (verification.ExpiresAt < now)
        {
            verification.Status = InternalTransferVerificationStatus.Expired;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>("Verification code has expired");
        }

        if (verification.Attempts >= 5)
        {
            verification.Status = InternalTransferVerificationStatus.Failed;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>("Too many invalid attempts");
        }

        var isCodeValid = BCrypt.Net.BCrypt.Verify(
            code,
            verification.CodeHash);

        if (!isCodeValid)
        {
            verification.Attempts += 1;

            if (verification.Attempts >= 5)
                verification.Status = InternalTransferVerificationStatus.Failed;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>("Invalid verification code");
        }

        if (verification.ReceiverUserId == senderUserId)
            return ResponseFactory.Fail<bool>("You cannot transfer to yourself");

        var senderWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == senderUserId &&
                x.WalletType == WalletType.Main);

        var receiverWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == verification.ReceiverUserId &&
                x.WalletType == WalletType.Main);

        if (senderWallet == null)
            return ResponseFactory.Fail<bool>("Sender Main Wallet not found");

        if (receiverWallet == null)
            return ResponseFactory.Fail<bool>("Receiver Main Wallet not found");

        if (senderWallet.IsLocked)
            return ResponseFactory.Fail<bool>("Sender Main Wallet is locked");

        if (senderWallet.Balance < verification.Amount)
            return ResponseFactory.Fail<bool>("Insufficient Main Wallet balance");

        var senderBefore = senderWallet.Balance;
        var receiverBefore = receiverWallet.Balance;

        senderWallet.Balance -= verification.Amount;
        receiverWallet.Balance += verification.Amount;

        verification.Status = InternalTransferVerificationStatus.Confirmed;
        verification.ConfirmedAt = now;

        var transferDescription = string.IsNullOrWhiteSpace(verification.Description)
            ? $"Internal transfer to UID {verification.ReceiverUser.ReferralCode}"
            : verification.Description;

        var receiverTransferDescription = string.IsNullOrWhiteSpace(verification.Description)
            ? $"Internal transfer from UID {verification.SenderUser.ReferralCode}"
            : verification.Description;

        var walletTransfer = new WalletTransfer
        {
            FromWalletId = senderWallet.WalletId,
            ToWalletId = receiverWallet.WalletId,
            Amount = verification.Amount,
            Description = verification.Description,
            CreatedAt = now
        };

        var senderTx = new WalletTransaction
        {
            UserId = senderUserId,
            WalletId = senderWallet.WalletId,
            Amount = -verification.Amount,
            BalanceBefore = senderBefore,
            BalanceAfter = senderWallet.Balance,
            Type = TransactionType.Transfer,
            Description = transferDescription,
            CreatedAt = now
        };

        var receiverTx = new WalletTransaction
        {
            UserId = verification.ReceiverUserId,
            WalletId = receiverWallet.WalletId,
            Amount = verification.Amount,
            BalanceBefore = receiverBefore,
            BalanceAfter = receiverWallet.Balance,
            Type = TransactionType.Transfer,
            Description = receiverTransferDescription,
            CreatedAt = now
        };

        _context.WalletTransfers.Add(walletTransfer);
        _context.WalletTransactions.AddRange(senderTx, receiverTx);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var receiverDisplay = BuildUserDisplay(
            verification.ReceiverUser.FirstName,
            verification.ReceiverUser.LastName,
            verification.ReceiverUser.Email,
            verification.ReceiverUser.ReferralCode);

        var completedEmail = _emailTemplateService.GetInternalTransferCompletedEmail(
            verification.Amount,
            receiverDisplay);

        await _notificationQueue.QueueEmailAsync(
            verification.SenderUser.Email,
            "Internal Transfer Completed",
            completedEmail);

        return ResponseFactory.Success(true, "Internal transfer completed");
    }
    public async Task<ApiResponse<bool>> TransferAsync(
        TransferRequest request,
        long userId)
    {
        if (request == null)
            return ResponseFactory.Fail<bool>("Invalid request");

        if (request.Amount <= 0)
            return ResponseFactory.Fail<bool>("Invalid amount");

        if (request.FromWalletId == request.ToWalletId)
        {
            return ResponseFactory.Fail<bool>(
                "Source and destination wallets cannot be the same");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var fromWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.WalletId == request.FromWalletId);

        var toWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.WalletId == request.ToWalletId);

        if (fromWallet == null || toWallet == null)
            return ResponseFactory.Fail<bool>("Wallet not found");

        if (fromWallet.UserId != userId ||
            toWallet.UserId != userId)
        {
            return ResponseFactory.Fail<bool>(
                "Unauthorized wallet access");
        }

        /*
         * کنترل مسیرهای مجاز انتقال در Backend.
         * این کنترل نباید فقط به JavaScript وابسته باشد.
         */
        var isAllowedWalletPath =
            (
                fromWallet.WalletType == WalletType.Main &&
                toWallet.WalletType == WalletType.Invest
            )
            ||
            (
                fromWallet.WalletType == WalletType.Invest &&
                toWallet.WalletType == WalletType.Main
            )
            ||
            (
                fromWallet.WalletType == WalletType.Profit &&
                (
                    toWallet.WalletType == WalletType.Main ||
                    toWallet.WalletType == WalletType.Invest
                )
            )
            ||
            (
                fromWallet.WalletType == WalletType.Referral &&
                (
                    toWallet.WalletType == WalletType.Main ||
                    toWallet.WalletType == WalletType.Invest
                )
            );

        if (!isAllowedWalletPath)
        {
            return ResponseFactory.Fail<bool>(
                "This wallet transfer path is not allowed");
        }

        if (fromWallet.IsLocked)
            return ResponseFactory.Fail<bool>("Wallet is locked");

        if (fromWallet.Balance < request.Amount)
            return ResponseFactory.Fail<bool>("Insufficient balance");

        var isMainToInvest =
            fromWallet.WalletType == WalletType.Main &&
            toWallet.WalletType == WalletType.Invest;

        var isTransferToInvest =
            toWallet.WalletType == WalletType.Invest;

        var isInvestToMain =
            fromWallet.WalletType == WalletType.Invest &&
            toWallet.WalletType == WalletType.Main;

        Contract? investmentContract = null;
        var contractActivatedNow = false;
        /*
         * برای هر انتقال به Investing Wallet باید قرارداد
         * Active یا PendingActivation وجود داشته باشد.
         */
        if (isTransferToInvest)
        {
            investmentContract = await _context.Contracts
                .Where(x =>
                    x.UserId == userId &&
                    (
                        x.Status == ContractStatus.Active ||
                        x.Status == ContractStatus.PendingActivation
                    ))
                .OrderByDescending(x =>
                    x.Status == ContractStatus.Active)
                .ThenByDescending(x => x.ContractId)
                .FirstOrDefaultAsync();

            if (investmentContract == null)
            {
                return ResponseFactory.Fail<bool>(
                    "No signed investment agreement was found. Please sign the agreement first.");
            }

            /*
             * یک قرارداد Active باید حتماً تاریخ شروع و پایان داشته باشد.
             * این کنترل از ادامه عملیات روی داده خراب جلوگیری می‌کند.
             */
            if (investmentContract.Status == ContractStatus.Active &&
                (
                    investmentContract.StartDate == null ||
                    investmentContract.EndDate == null
                ))
            {
                return ResponseFactory.Fail<bool>(
                    "Active contract dates are invalid. Please contact support.");
            }
        }

        /*
         * تا زمانی که قرارداد Active است،
         * انتقال اصل سرمایه از Invest به Main مجاز نیست.
         */
        if (isInvestToMain)
        {
            var activeContractExists =
                await _context.Contracts.AnyAsync(x =>
                    x.UserId == userId &&
                    x.Status == ContractStatus.Active &&
                    (
                        x.EndDate == null ||
                        x.EndDate > DateTime.Now
                    ));

            if (activeContractExists)
            {
                return ResponseFactory.Fail<bool>(
                    "Contract is still active. Investment principal cannot be transferred to the Main Wallet.");
            }
        }

        /*
         * رزرو ظرفیت قبل از تغییر موجودی‌ها انجام می‌شود.
         * ReserveCurrentMonthCapacityAsync فقط Entity را تغییر می‌دهد
         * و SaveChanges جداگانه اجرا نمی‌کند؛ بنابراین بخشی از همین
         * Transaction باقی می‌ماند.
         */
        if (isTransferToInvest)
        {
            var capacityResult =
                await _investmentCapacityService
                    .ReserveCurrentMonthCapacityAsync(
                        request.Amount);

            if (!capacityResult.Success)
            {
                return ResponseFactory.Fail<bool>(
                    capacityResult.Message);
            }
        }

        var now = DateTime.Now;

        var fromBefore = fromWallet.Balance;
        var toBefore = toWallet.Balance;

        InvestmentWaitListEntry? completedWaitListEntry = null;

        fromWallet.Balance -= request.Amount;
        toWallet.Balance += request.Amount;

        /*
         * فقط قرارداد PendingActivation فعال می‌شود.
         * قرارداد Active قبلی هرگز دوباره تاریخ‌گذاری نمی‌شود.
         */
        if (isTransferToInvest &&
            investmentContract != null &&
            investmentContract.Status ==
                ContractStatus.PendingActivation)
        {
            investmentContract.Status =
                ContractStatus.Active;

            investmentContract.AcceptedAt ??= now;
            investmentContract.StartDate = now;
            investmentContract.EndDate =
                now.AddMonths(6);
            contractActivatedNow = true;
            /*
             * اصل سرمایه تا پایان قرارداد قفل می‌شود.
             * ورود وجه به کیف پول قفل‌شده همچنان ممکن است؛
             * فقط انتقال از آن محدود می‌شود.
             */
            toWallet.IsLocked = true;
        }

        /*
         * Referral Commission فقط برای مسیر Main → Invest
         * محاسبه می‌شود.
         */
        if (isMainToInvest)
        {
            await _mainInvestBonusService.HandleBonusAsync(
                userId,
                request.Amount);
        }

        if (isTransferToInvest)
        {
            completedWaitListEntry =
                await _context.InvestmentWaitListEntries
                    .Where(x =>
                        x.UserId == userId &&
                        x.Status ==
                            InvestmentWaitListStatus.CapacityAvailable &&
                        x.RequestedAmount <= request.Amount)
                    .OrderBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

            if (completedWaitListEntry != null)
            {
                completedWaitListEntry.Status =
                    InvestmentWaitListStatus.Completed;

                completedWaitListEntry.CompletedAt = now;

                completedWaitListEntry.AdminNote =
                    "Completed automatically after transfer to Investing Wallet.";
            }
        }

        var transferDescription =
            string.IsNullOrWhiteSpace(request.Description)
                ? $"Transfer from {fromWallet.WalletType} to {toWallet.WalletType}"
                : request.Description.Trim();

        var transfer = new WalletTransfer
        {
            FromWalletId = fromWallet.WalletId,
            ToWalletId = toWallet.WalletId,
            Amount = request.Amount,
            Description = transferDescription,
            CreatedAt = now
        };

        var fromTx = new WalletTransaction
        {
            UserId = fromWallet.UserId,
            WalletId = fromWallet.WalletId,
            Amount = -request.Amount,
            BalanceBefore = fromBefore,
            BalanceAfter = fromWallet.Balance,
            Type = TransactionType.Transfer,
            Description = transferDescription,
            CreatedAt = now
        };

        var toTx = new WalletTransaction
        {
            UserId = toWallet.UserId,
            WalletId = toWallet.WalletId,
            Amount = request.Amount,
            BalanceBefore = toBefore,
            BalanceAfter = toWallet.Balance,
            Type = TransactionType.Transfer,
            Description = transferDescription,
            CreatedAt = now
        };

        _context.WalletTransfers.Add(transfer);

        _context.WalletTransactions.AddRange(
            fromTx,
            toTx);

        /*
         * تغییر موجودی، ظرفیت، فعال‌شدن قرارداد،
         * قفل Wallet، Bonus و Wait List همگی با
         * یک SaveChanges ذخیره می‌شوند.
         */
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        /*
         * ارسال ایمیل بعد از Commit انجام می‌شود.
         * شکست Queue ایمیل نباید Transaction مالی را Rollback کند.
         */
        if (isTransferToInvest)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

            if (user != null)
            {
                var emailBody =
                    _emailTemplateService
                        .GetInvestWalletTransferEmail(
                            request.Amount,
                            fromWallet.WalletType.ToString(),
                            toWallet.Balance);

                await _notificationQueue.QueueEmailAsync(
                    user.Email,
                    "Investment Wallet Updated",
                    emailBody);
            }
        }

        var successMessage = contractActivatedNow
            ? "Transfer completed and the six-month investment contract was activated."
            : "Transfer completed";

        return ResponseFactory.Success(
            true,
            successMessage);
    }

    public async Task<ApiResponse<bool>> CreateDepositRequestAsync(
        DepositRequest request,
        long userId)
    {
        if (request == null)
            return ResponseFactory.Fail<bool>("Invalid request");

        if (request.Amount <= 0)
            return ResponseFactory.Fail<bool>("Invalid amount");

        var txId = _paymentTransactionGuardService.NormalizeTxId(request.TxId);

        var txValidationError = _paymentTransactionGuardService.ValidateTxId(txId);

        if (txValidationError != null)
            return ResponseFactory.Fail<bool>(txValidationError);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var txIdAlreadyUsed = await _paymentTransactionGuardService.IsTxIdUsedAsync(txId);

        if (txIdAlreadyUsed)
            return ResponseFactory.Fail<bool>("Transaction hash is already used");

        var siteWalletAddress = await _settingService.GetValueAsync("SiteWalletAddress");
        var network = await _settingService.GetValueAsync("Network");

        var deposit = new Deposit
        {
            UserId = userId,
            Amount = request.Amount,
            TxId = txId,
            SiteWalletAddress = siteWalletAddress,
            Network = network,
            Status = DepositStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _context.Deposits.Add(deposit);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        var userDisplay = user != null
            ? $"{user.Email} / UID: {user.ReferralCode}"
            : $"UserId: {userId}";

        var emailBody = _emailTemplateService.GetDepositNotification(
            userDisplay,
            request.Amount,
            txId);

        await _notificationQueue.QueueAdminAsync(
            "New Deposit Request",
            emailBody);

        return ResponseFactory.Success(true, "Deposit request submitted");
    }

    public async Task<ApiResponse<WithdrawalVerificationResponse>>CreateWithdrawalRequestAsync(
            WithdrawalRequest request,
            long userId)
    {
        if (
            request == null ||
            request.Amount <= 0
        )
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Invalid withdrawal amount");
        }

        var withdrawLimitValue =
            await _settingService
                .GetValueAsync(
                    "WithdrawalLimit");

        if (string.IsNullOrWhiteSpace(
                withdrawLimitValue))
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Withdraw limit not set from admin");
        }

        if (!decimal.TryParse(
                withdrawLimitValue,
                out var withdrawalLimit))
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Invalid withdraw limit setting");
        }

        if (request.Amount < withdrawalLimit)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    $"Invalid amount. Amount should be " +
                    $"{withdrawalLimit} USDT or more");
        }

        var user =
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "User not found");
        }

        if (user.MustChangePassword)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "You must change your password before requesting a withdrawal.");
        }

        var securityNow =
            DateTime.UtcNow;

        if (
            user.TotpRecoveryLockedUntil.HasValue &&
            user.TotpRecoveryLockedUntil.Value >
            securityNow
        )
        {
            var lockedUntil =
                user.TotpRecoveryLockedUntil.Value
                    .ToString(
                        "yyyy-MM-dd HH:mm 'UTC'");

            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    $"Withdrawals are temporarily locked until {lockedUntil} after two-factor account recovery.");
        }

        var hasActiveRecovery =
            await HasActiveTwoFactorRecoveryAsync(
                userId,
                securityNow);

        if (hasActiveRecovery)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Withdrawals are unavailable while a two-factor recovery request is being verified or reviewed.");
        }

        if (user.KycStatus != KycStatus.Approved)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "KYC approval required");
        }

        var wallet =
            await _context.Wallets
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.WalletType ==
                    WalletType.Main);

        if (wallet == null)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Main wallet not found");
        }

        if (wallet.IsLocked)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Main wallet is locked");
        }

        if (wallet.Balance < request.Amount)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Insufficient balance");
        }

        var address =
            await _context
                .UserWalletAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (address == null)
        {
            return ResponseFactory
                .Fail<WithdrawalVerificationResponse>(
                    "Withdrawal wallet address not found");
        }

        var now =
            DateTime.Now;

        var expiresInMinutes =
            10;

        var verificationMethod =
            user.IsTotpEnabled
                ? WithdrawalVerificationMethod.Totp
                : WithdrawalVerificationMethod.Email;

        var activeVerifications =
            await _context
                .WithdrawalVerifications
                .Where(x =>
                    x.UserId == userId &&
                    x.Status ==
                    WithdrawalVerificationStatus.Pending)
                .ToListAsync();

        foreach (
            var activeVerification in
            activeVerifications
        )
        {
            activeVerification.Status =
                WithdrawalVerificationStatus
                    .Expired;
        }

        string? emailCode =
            null;

        string codeHash =
            string.Empty;

        if (
            verificationMethod ==
            WithdrawalVerificationMethod.Email
        )
        {
            emailCode =
                GenerateInternalTransferCode();

            codeHash =
                BCrypt.Net.BCrypt
                    .HashPassword(
                        emailCode);
        }

        var verification =
            new WithdrawalVerification
            {
                UserId =
                    userId,

                Amount =
                    request.Amount,

                WalletAddress =
                    address.Address,

                Network =
                    address.Network,

                VerificationMethod =
                    verificationMethod,

                CodeHash =
                    codeHash,

                Status =
                    WithdrawalVerificationStatus
                        .Pending,

                Attempts =
                    0,

                CreatedAt =
                    now,

                ExpiresAt =
                    now.AddMinutes(
                        expiresInMinutes)
            };

        _context.WithdrawalVerifications.Add(
            verification);

        await _context.SaveChangesAsync();

        if (
            verificationMethod ==
            WithdrawalVerificationMethod.Email
        )
        {
            var emailBody =
                _emailTemplateService
                    .GetWithdrawalVerificationEmail(
                        request.Amount,
                        address.Address,
                        address.Network,
                        emailCode!,
                        expiresInMinutes);

            await _notificationQueue.QueueEmailAsync(
                user.Email,
                "Withdrawal Verification Code",
                emailBody);
        }

        var response =
            new WithdrawalVerificationResponse
            {
                VerificationId =
                    verification
                        .WithdrawalVerificationId,

                Amount =
                    verification.Amount,

                WalletAddress =
                    verification.WalletAddress,

                Network =
                    verification.Network,

                ExpiresInMinutes =
                    expiresInMinutes,

                VerificationMethod =
                    verificationMethod.ToString(),

                RequiresEmailCode =
                    verificationMethod ==
                    WithdrawalVerificationMethod.Email
            };

        var message =
            verificationMethod ==
            WithdrawalVerificationMethod.Totp
                ? "Enter the current code from Google Authenticator."
                : "Verification code sent to your email.";

        return ResponseFactory.Success(
            response,
            message);
    }
    public async Task<ApiResponse<bool>>
        ConfirmWithdrawalRequestAsync(
            ConfirmWithdrawalRequest request,
            long userId)
    {
        var code =
            request?.Code?.Trim()
            ?? string.Empty;

        if (
            request == null ||
            request.VerificationId <= 0
        )
        {
            return ResponseFactory.Fail<bool>(
                "Verification is required");
        }

        if (
            code.Length != 6 ||
            !code.All(char.IsDigit)
        )
        {
            return ResponseFactory.Fail<bool>(
                "Verification code must be 6 digits");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable);

        var verification =
            await _context
                .WithdrawalVerifications
                .Include(x =>
                    x.User)
                .FirstOrDefaultAsync(x =>
                    x.WithdrawalVerificationId ==
                    request.VerificationId &&
                    x.UserId == userId);

        if (verification == null)
        {
            return ResponseFactory.Fail<bool>(
                "Verification request not found");
        }

        if (
            verification.Status !=
            WithdrawalVerificationStatus.Pending
        )
        {
            return ResponseFactory.Fail<bool>(
                "Verification request is not pending");
        }

        var now =
            DateTime.Now;

        var securityNow =
            DateTime.UtcNow;

        if (verification.ExpiresAt <= now)
        {
            verification.Status =
                WithdrawalVerificationStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>(
                "Verification request has expired");
        }

        if (verification.User.MustChangePassword)
        {
            verification.Status =
                WithdrawalVerificationStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>(
                "You must change your password before requesting a withdrawal.");
        }

        if (
            verification.User
                .TotpRecoveryLockedUntil
                .HasValue &&
            verification.User
                .TotpRecoveryLockedUntil
                .Value >
            securityNow
        )
        {
            verification.Status =
                WithdrawalVerificationStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            var lockedUntil =
                verification.User
                    .TotpRecoveryLockedUntil
                    .Value
                    .ToString(
                        "yyyy-MM-dd HH:mm 'UTC'");

            return ResponseFactory.Fail<bool>(
                $"Withdrawals are temporarily locked until {lockedUntil} after two-factor account recovery.");
        }

        var hasActiveRecovery =
            await HasActiveTwoFactorRecoveryAsync(
                userId,
                securityNow);

        if (hasActiveRecovery)
        {
            verification.Status =
                WithdrawalVerificationStatus
                    .Expired;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>(
                "Withdrawals are unavailable while a two-factor recovery request is being reviewed.");
        }

        if (verification.Attempts >= 5)
        {
            verification.Status =
                WithdrawalVerificationStatus
                    .Failed;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>(
                "Too many invalid attempts");
        }

        var isCodeValid =
            false;

        var invalidCodeMessage =
            "Invalid verification code";

        if (
            verification.VerificationMethod ==
            WithdrawalVerificationMethod.Totp
        )
        {
            if (!verification.User.IsTotpEnabled)
            {
                verification.Status =
                    WithdrawalVerificationStatus
                        .Expired;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return ResponseFactory.Fail<bool>(
                    "Google Authenticator settings changed. Start a new withdrawal request.");
            }

            var totpResult =
                await _twoFactorAuthenticationService
                    .VerifyWithdrawalTotpAsync(
                        userId,
                        code);

            isCodeValid =
                totpResult.Success;

            invalidCodeMessage =
                totpResult.Message;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(
                    verification.CodeHash))
            {
                verification.Status =
                    WithdrawalVerificationStatus
                        .Failed;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return ResponseFactory.Fail<bool>(
                    "Email verification is unavailable. Start a new withdrawal request.");
            }

            isCodeValid =
                BCrypt.Net.BCrypt.Verify(
                    code,
                    verification.CodeHash);
        }

        if (!isCodeValid)
        {
            verification.Attempts +=
                1;

            if (verification.Attempts >= 5)
            {
                verification.Status =
                    WithdrawalVerificationStatus
                        .Failed;
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return ResponseFactory.Fail<bool>(
                invalidCodeMessage);
        }

        var wallet =
            await _context.Wallets
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.WalletType ==
                    WalletType.Main);

        if (wallet == null)
        {
            return ResponseFactory.Fail<bool>(
                "Main wallet not found");
        }

        if (wallet.IsLocked)
        {
            return ResponseFactory.Fail<bool>(
                "Main wallet is locked");
        }

        if (wallet.Balance <
            verification.Amount)
        {
            return ResponseFactory.Fail<bool>(
                "Insufficient balance");
        }

        var balanceBefore =
            wallet.Balance;

        var balanceAfter =
            balanceBefore -
            verification.Amount;

        wallet.Balance =
            balanceAfter;

        verification.Status =
            WithdrawalVerificationStatus
                .Confirmed;

        verification.ConfirmedAt =
            now;

        var withdrawal =
            new Withdrawal
            {
                UserId =
                    userId,

                Amount =
                    verification.Amount,

                WalletAddress =
                    verification.WalletAddress,

                Network =
                    verification.Network,

                Status =
                    DepositStatus.Pending,

                CreatedAt =
                    now
            };

        _context.Withdrawals.Add(
            withdrawal);

        var verificationDescription =
            verification.VerificationMethod ==
            WithdrawalVerificationMethod.Totp
                ? "Withdrawal request reserved after Google Authenticator verification"
                : "Withdrawal request reserved after email verification";

        _context.WalletTransactions.Add(
            new WalletTransaction
            {
                UserId =
                    userId,

                WalletId =
                    wallet.WalletId,

                Amount =
                    -verification.Amount,

                BalanceBefore =
                    balanceBefore,

                BalanceAfter =
                    balanceAfter,

                Type =
                    TransactionType.Withdrawal,

                Description =
                    verificationDescription,

                CreatedAt =
                    now
            });

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var adminEmailBody =
            _emailTemplateService
                .GetWithdrawalNotification(
                    verification.User.Email,
                    verification.Amount,
                    verification.WalletAddress);

        await _notificationQueue.QueueAdminAsync(
            "New Withdrawal Request",
            adminEmailBody);

        var userEmailBody =
            _emailTemplateService
                .GetWithdrawalSubmittedEmail(
                    verification.Amount,
                    verification.WalletAddress);

        await _notificationQueue.QueueEmailAsync(
            verification.User.Email,
            "Withdrawal Request Submitted",
            userEmailBody);

        return ResponseFactory.Success(
            true,
            "Withdrawal request submitted");
    }
    public async Task<ApiResponse<bool>> SaveWalletAddressAsync(
        string address,
        string network,
        long userId)
    {
        var normalizedAddress =
            address?.Trim() ?? string.Empty;

        var normalizedNetwork =
            network?.Trim().ToUpperInvariant()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return ResponseFactory.Fail<bool>(
                "Wallet address is required");
        }

        /*
         * فقط BSC با استاندارد BEP20 مجاز است.
         * هر دو عنوان BSC و BEP20 از Frontend پذیرفته می‌شوند،
         * اما مقدار ذخیره‌شده همیشه BEP20 خواهد بود.
         */
        if (
            normalizedNetwork != "BEP20" &&
            normalizedNetwork != "BSC"
        )
        {
            return ResponseFactory.Fail<bool>(
                "Only USDT on the BNB Smart Chain (BEP20) network is supported");
        }

        /*
         * آدرس‌های BSC/EVM باید با 0x شروع شوند
         * و در مجموع 42 کاراکتر داشته باشند.
         */
        var isValidBscAddress =
            normalizedAddress.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase) &&
            normalizedAddress.Length == 42 &&
            normalizedAddress
                .Skip(2)
                .All(Uri.IsHexDigit);

        if (!isValidBscAddress)
        {
            return ResponseFactory.Fail<bool>(
                "Please enter a valid USDT BEP20 withdrawal address");
        }

        var existing = await _context.UserWalletAddresses
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (existing != null)
        {
            existing.Address =
                normalizedAddress;

            existing.Network =
                "BEP20";
        }
        else
        {
            var walletAddress =
                new UserWalletAddress
                {
                    UserId = userId,
                    Address = normalizedAddress,
                    Network = "BEP20"
                };

            _context.UserWalletAddresses.Add(
                walletAddress);
        }

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            true,
            "USDT BEP20 withdrawal address saved successfully");
    }
    public async Task<ApiResponse<UserProfitsResponse>> GetUserProfitsAsync(
        long userId,
        int page,
        int pageSize,
        string? type)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;
        pageSize = pageSize > 50 ? 50 : pageSize;

        var normalizedType = type?.Trim().ToLower();

        var totalInvestmentProfit = await _context.ProfitLedgers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Type == ProfitType.Investment)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalReferralProfit = await _context.ProfitLedgers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Type == ProfitType.Referral)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var query = _context.ProfitLedgers
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (normalizedType == "investment")
        {
            query = query.Where(x => x.Type == ProfitType.Investment);
        }
        else if (normalizedType == "referral")
        {
            query = query.Where(x => x.Type == ProfitType.Referral);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserProfitHistoryDto
            {
                Id = x.Id,
                Type = x.Type == ProfitType.Investment
                    ? "Investment"
                    : "Referral",

                ReferenceId = x.ReferenceId,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt,

                RealCapitalAmount = x.RealCapitalAmount,
                BonusCapitalAmount = x.BonusCapitalAmount,
                ProfitBaseAmount = x.ProfitBaseAmount,

                EffectiveRankName = x.EffectiveRank != null
                    ? x.EffectiveRank.Name
                    : null,

                SourceUserUid = x.SourceUser != null
                    ? x.SourceUser.ReferralCode
                    : null,

                SourceUserEmail = x.SourceUser != null
                    ? x.SourceUser.Email
                    : null,

                SourceUserFullName = x.SourceUser != null
                    ? (x.SourceUser.FirstName + " " + x.SourceUser.LastName).Trim()
                    : null
            })
            .ToListAsync();

        var response = new UserProfitsResponse
        {
            TotalProfit = totalInvestmentProfit + totalReferralProfit,
            InvestmentProfit = totalInvestmentProfit,
            ReferralProfit = totalReferralProfit,
            History = new PagedResponse<UserProfitHistoryDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = items
            }
        };

        return ResponseFactory.Success(response);
    }
    public async Task<ApiResponse<ReferralOverviewResponse>> GetReferralOverviewAsync(
        long userId,
        int referralsPage,
        int referralsPageSize,
        int bonusesPage,
        int bonusesPageSize)
    {
        referralsPage = referralsPage <= 0 ? 1 : referralsPage;
        bonusesPage = bonusesPage <= 0 ? 1 : bonusesPage;

        referralsPageSize = referralsPageSize <= 0 ? 10 : referralsPageSize;
        bonusesPageSize = bonusesPageSize <= 0 ? 10 : bonusesPageSize;

        referralsPageSize = referralsPageSize > 50 ? 50 : referralsPageSize;
        bonusesPageSize = bonusesPageSize > 50 ? 50 : bonusesPageSize;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<ReferralOverviewResponse>("User not found");

        var rank = await _context.Ranks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

        var referralWallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.WalletType == WalletType.Referral);

        var referralsQuery = _context.Users
            .AsNoTracking()
            .Where(x => x.SponsorId == userId);

        var totalReferrals = await referralsQuery.CountAsync();

        var referrals = await referralsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((referralsPage - 1) * referralsPageSize)
            .Take(referralsPageSize)
            .Select(x => new DirectReferralDto
            {
                UserId = x.UserId,
                FullName = (x.FirstName + " " + x.LastName).Trim(),
                Email = x.Email,
                ReferralCode = x.ReferralCode,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var bonusesQuery = _context.ReferralBonusRecords
            .AsNoTracking()
            .Include(x => x.FromUser)
            .Where(x => x.ToUserId == userId);

        var totalBonusRecords = await bonusesQuery.CountAsync();

        var totalBonus = await bonusesQuery
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var bonuses = await bonusesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((bonusesPage - 1) * bonusesPageSize)
            .Take(bonusesPageSize)
            .Select(x => new ReferralBonusHistoryDto
            {
                BonusId = x.BonusId,
                Amount = x.Amount,
                Type = x.Type,
                CreatedAt = x.CreatedAt,

                FromUserUid = x.FromUser != null
                    ? x.FromUser.ReferralCode
                    : string.Empty,

                FromUserEmail = x.FromUser != null
                    ? x.FromUser.Email
                    : string.Empty,

                FromUserFullName = x.FromUser != null
                    ? (x.FromUser.FirstName + " " + x.FromUser.LastName).Trim()
                    : string.Empty
            })
            .ToListAsync();

        var response = new ReferralOverviewResponse
        {
            ReferralCode = user.ReferralCode,
            CurrentRank = rank != null ? rank.Name : "-",
            ReferralProfitPercent = rank != null ? rank.ReferralProfitPercent : 0,
            MainToInvestPercent = rank != null ? rank.MainToInvestPercent : 0,
            ReferralWalletBalance = referralWallet != null ? referralWallet.Balance : 0,
            TotalReferrals = totalReferrals,
            TotalBonus = totalBonus,

            Referrals = new PagedResponse<DirectReferralDto>
            {
                Page = referralsPage,
                PageSize = referralsPageSize,
                TotalCount = totalReferrals,
                TotalPages = (int)Math.Ceiling(totalReferrals / (double)referralsPageSize),
                Items = referrals
            },

            Bonuses = new PagedResponse<ReferralBonusHistoryDto>
            {
                Page = bonusesPage,
                PageSize = bonusesPageSize,
                TotalCount = totalBonusRecords,
                TotalPages = (int)Math.Ceiling(totalBonusRecords / (double)bonusesPageSize),
                Items = bonuses
            }
        };

        return ResponseFactory.Success(response);
    }
    public async Task<ApiResponse<object>> GetDepositInfoAsync()
    {
        var walletAddress = await _settingService.GetValueAsync("SiteWalletAddress");
        var network = await _settingService.GetValueAsync("Network");

        return ResponseFactory.Success<object>(new
        {
            SiteWalletAddress = walletAddress ?? "",
            Network = network ?? ""
        });
    }

    public async Task<ApiResponse<PagedResponse<UserPendingWithdrawalDto>>> GetMyWithdrawalsAsync(
        long userId,
        int page = 1,
        int pageSize = 20)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.Withdrawals
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var totalCount = await query.CountAsync();

        var withdrawals = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserPendingWithdrawalDto
            {
                WithdrawalId = x.WithdrawalId,
                Amount = x.Amount,
                WalletAddress = x.WalletAddress,
                Network = x.Network,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CanCancel = x.Status == DepositStatus.Pending,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var response = new PagedResponse<UserPendingWithdrawalDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = withdrawals
        };

        return ResponseFactory.Success(response);
    }
    public async Task<ApiResponse<bool>> CancelWithdrawalAsync(long withdrawalId, long userId)
    {
        var withdrawal = await _context.Withdrawals
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.WithdrawalId == withdrawalId &&
                x.UserId == userId);

        if (withdrawal == null)
            return ResponseFactory.Fail<bool>("Withdrawal request not found");

        if (withdrawal.Status != DepositStatus.Pending)
            return ResponseFactory.Fail<bool>("Only pending withdrawals can be canceled");

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.WalletType == WalletType.Main);

        if (wallet == null)
            return ResponseFactory.Fail<bool>("Main wallet not found");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var now = DateTime.Now;

        var before = wallet.Balance;
        var after = before + withdrawal.Amount;

        wallet.Balance = after;

        withdrawal.Status = DepositStatus.Canceled;
        withdrawal.AdminNote = "Canceled by user";
        withdrawal.ProcessedAt = now;

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = userId,
            WalletId = wallet.WalletId,
            Amount = withdrawal.Amount,
            BalanceBefore = before,
            BalanceAfter = after,
            Type = TransactionType.Adjustment,
            Description = "Withdrawal request canceled by user",
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var emailBody = _emailTemplateService.GetWithdrawalCanceledEmail(
            withdrawal.Amount);

        await _notificationQueue.QueueEmailAsync(
            withdrawal.User.Email,
            "Withdrawal Canceled",
            emailBody);

        return ResponseFactory.Success(true, "Withdrawal request canceled");
    }
    public async Task<ApiResponse<PagedResponse<UserDepositHistoryDto>>> GetMyDepositsAsync(
        long userId,
        int page = 1,
        int pageSize = 20)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.Deposits
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var totalCount = await query.CountAsync();

        var deposits = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserDepositHistoryDto
            {
                DepositId = x.DepositId,
                Amount = x.Amount,
                TxId = x.TxId,
                Status = x.Status.ToString(),
                SiteWalletAddress = x.SiteWalletAddress,
                Network = x.Network,
                AdminNote = x.AdminNote,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var response = new PagedResponse<UserDepositHistoryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = deposits
        };

        return ResponseFactory.Success(response);
    }
    private async Task<bool>
    HasActiveTwoFactorRecoveryAsync(
        long userId,
        DateTime utcNow)
    {
        return await _context
            .TwoFactorRecoveryRequests
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                (
                    (
                        x.Status ==
                        TwoFactorRecoveryRequestStatus
                            .PendingEmailVerification &&
                        x.EmailCodeExpiresAt >
                        utcNow
                    )
                    ||
                    (
                        x.Status ==
                        TwoFactorRecoveryRequestStatus
                            .PendingAdminReview &&
                        x.ExpiresAt >
                        utcNow
                    )
                ));
    }
    private static string GenerateInternalTransferCode()
    {
        return RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();
    }

    private static string BuildUserDisplay(
        string firstName,
        string lastName,
        string email,
        string referralCode)
    {
        var fullName = $"{firstName} {lastName}".Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "-";

        return $"{fullName} / {email} / UID: {referralCode}";
    }
}