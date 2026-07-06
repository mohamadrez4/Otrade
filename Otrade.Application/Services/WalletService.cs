using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Wallet;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System;
 
namespace Otrade.Application.Services;

public class WalletService
{
    private readonly OtradeDbContext _context;
    private readonly MainInvestBonusService _mainInvestBonusService;
    private readonly SystemSettingService _settingService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IServiceScopeFactory _scopeFactory;
    public WalletService(OtradeDbContext context,

        IServiceScopeFactory scopeFactory,
        MainInvestBonusService mainInvestBonusService,
        SystemSettingService systemSettingService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _mainInvestBonusService = mainInvestBonusService;
        _settingService= systemSettingService;
        _emailService= emailService;
        _emailTemplateService= emailTemplateService;
    }

    public async Task<ApiResponse<bool>> TransferAsync(
        TransferRequest request,
        long userId)
    {
        if (request.Amount <= 0)
            return ResponseFactory.Fail<bool>("Invalid amount");

        var fromWallet = await _context.Wallets
            .FirstOrDefaultAsync(x => x.WalletId == request.FromWalletId);

        var toWallet = await _context.Wallets
            .FirstOrDefaultAsync(x => x.WalletId == request.ToWalletId);

        if (fromWallet == null || toWallet == null)
            return ResponseFactory.Fail<bool>("Wallet not found");

        // Ownership check
        if (toWallet.UserId != userId)
            return ResponseFactory.Fail<bool>("Unauthorized wallet access");
        if (fromWallet.UserId != userId)
            return ResponseFactory.Fail<bool>("Unauthorized wallet access");

        if (fromWallet.IsLocked)
            return ResponseFactory.Fail<bool>("Wallet is locked");

        if (fromWallet.Balance < request.Amount)
            return ResponseFactory.Fail<bool>("Insufficient balance");
        if (fromWallet.WalletType == WalletType.Invest &&
            toWallet.WalletType == WalletType.Main)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Status == ContractStatus.Active);

            if (contract != null)
                return ResponseFactory.Fail<bool>(
                    "Contract is still active. Withdraw not allowed.");
        }
        if (fromWallet.WalletType == WalletType.Main &&
            toWallet.WalletType == WalletType.Invest)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Status == ContractStatus.Active);

            if (contract == null)
            {
                return ResponseFactory.Fail<bool>(
                    "No active contract. Please create a contract first.");
            }
        }
        var fromBefore = fromWallet.Balance;
        var toBefore = toWallet.Balance;

        // Update balances
        fromWallet.Balance -= request.Amount;
        toWallet.Balance += request.Amount;
        if (fromWallet.WalletType == WalletType.Main &&
            toWallet.WalletType == WalletType.Invest)
        {
            await _mainInvestBonusService.HandleBonusAsync(
                userId,
                request.Amount);
        }
        // WalletTransfer (Business Event)
        var transfer = new WalletTransfer
        {
            FromWalletId = fromWallet.WalletId,
            ToWalletId = toWallet.WalletId,
            Amount = request.Amount,
            CreatedAt = DateTime.Now
        };

        // Transaction Log - FROM
        var fromTx = new WalletTransaction
        {
            UserId = fromWallet.UserId,
            WalletId = fromWallet.WalletId,
            Amount = -request.Amount,
            BalanceBefore = fromBefore,
            BalanceAfter = fromWallet.Balance,
            Type = TransactionType.Transfer,
            Description = request.Description,
            CreatedAt =  DateTime.Now
        };

        // Transaction Log - TO
        var toTx = new WalletTransaction
        {
            UserId = toWallet.UserId,
            WalletId = toWallet.WalletId,
            Amount = request.Amount,
            BalanceBefore = toBefore,
            BalanceAfter = toWallet.Balance,
            Type = TransactionType.Transfer,
            Description = request.Description,
            CreatedAt =  DateTime.Now
        };

        _context.WalletTransfers.Add(transfer);
        _context.WalletTransactions.AddRange(fromTx, toTx);

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "Transfer completed");
    }

    public async Task<ApiResponse<bool>> CreateDepositRequestAsync(
    DepositRequest request,
    long userId)
    {
        if (request.Amount <= 0)
            return ResponseFactory.Fail<bool>("Invalid amount");

        var exists = await _context.Deposits
            .AnyAsync(x => x.TxId == request.TxId);

        if (exists)
            return ResponseFactory.Fail<bool>("TxId already used");

        var deposit = new Deposit
        {
            UserId = userId,
            Amount = request.Amount,
            TxId = request.TxId,
            Status =DepositStatus.Pending,
            CreatedAt =  DateTime.Now
        };

        _context.Deposits.Add(deposit);
        await _context.SaveChangesAsync();
        var adminEmail =await _settingService.GetValueAsync("ADMIN_EMAIL");

        var requestamount = request.Amount;
        var requesttxid = request.TxId;
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                        adminEmail,
                        "New Deposit Request",
                        emailTemplateService.GetDepositNotification(
                            userId.ToString(),
                            requestamount,
                            requesttxid));
                }
                catch
                {
                }
            });
        }
        return ResponseFactory.Success(true, "Deposit request submitted");
    }

    public async Task<ApiResponse<bool>> CreateWithdrawalRequestAsync(
    WithdrawalRequest request,
    long userId)
    {
        var withdrawlimit = await _settingService.GetValueAsync("WithdrawalLimit");

        if (string.IsNullOrWhiteSpace(withdrawlimit))
            return ResponseFactory.Fail<bool>("Withdraw limit not set from admin");

        if (request.Amount < int.Parse(withdrawlimit))
            return ResponseFactory.Fail<bool>("Invalid amount. Amount should be 100 Tether Or More");

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<bool>("User not found");

        if (user.KycStatus != KycStatus.Approved)
            return ResponseFactory.Fail<bool>("KYC approval required");

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.WalletType == WalletType.Main);

        if (wallet == null)
            return ResponseFactory.Fail<bool>("Main wallet not found");

        if (wallet.Balance < request.Amount)
            return ResponseFactory.Fail<bool>("Insufficient balance");

        var address = await _context.UserWalletAddresses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (address == null)
            return ResponseFactory.Fail<bool>("Wallet address not found");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var before = wallet.Balance;
        var after = before - request.Amount;

        wallet.Balance = after;

        var withdrawal = new Withdrawal
        {
            UserId = userId,
            Amount = request.Amount,
            WalletAddress = address.Address,
            Network = address.Network,
            Status = DepositStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _context.Withdrawals.Add(withdrawal);

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = userId,
            WalletId = wallet.WalletId,
            Amount = -request.Amount,
            BalanceBefore = before,
            BalanceAfter = after,
            Type = TransactionType.Withdrawal,
            Description = "Withdrawal request reserved",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        var adminEmail = await _settingService.GetValueAsync("ADMIN_EMAIL");

        var useremail = user.Email;
        var requestamount = request.Amount;
        var address_address = address.Address;
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                        adminEmail,
                        "New Withdrawal Request",
                        emailTemplateService.GetWithdrawalNotification(
                            useremail,
                            requestamount,
                            address_address)
                    );

                }
                catch (Exception ex)
                {
                    //using var scope = _scopeFactory.CreateScope();

                    //var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    //_context.EmailLogs.Add(new EmailLog
                    //{
                    //    ToEmail = useremail,
                    //    Subject = "New Withdrawal Request",
                    //    Body = emailTemplateService.GetWithdrawalNotification(
                    //        useremail,
                    //        requestamount,
                    //        address_address),
                    //    CreatedAt =  DateTime.Now,
                    //    Status="Faild"
                    //});
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
                }
            });
        }
        return ResponseFactory.Success(true, "Withdrawal request submitted");
    }
    //public async Task<ApiResponse<bool>> CreateWithdrawalRequestAsync(
    //    WithdrawalRequest request,
    //    long userId)
    //{ 
    //    var withdrawlimit = await _settingService.GetValueAsync("WithdrawalLimit");

    //    if (withdrawlimit == null || withdrawlimit=="")
    //        return ResponseFactory.Fail<bool>("Withrawlimit Not Set From Admin");

    //    if (request.Amount < int.Parse(withdrawlimit) )
    //        return ResponseFactory.Fail<bool>("Minimum withdrawal amount is 100 USDT.");
    //    var user = await _context.Users
    //        .FirstOrDefaultAsync(x => x.UserId == userId);

    //    if (user == null)
    //        return ResponseFactory.Fail<bool>("User not found");

    //    if (user.KycStatus != KycStatus.Approved)
    //        return ResponseFactory.Fail<bool>("KYC approval required");
    //    var wallet = await _context.Wallets
    //        .FirstOrDefaultAsync(x =>
    //            x.UserId == userId &&
    //            x.WalletType == WalletType.Main);

    //    if (wallet == null)
    //        return ResponseFactory.Fail<bool>("Main wallet not found");

    //    if (wallet.Balance < request.Amount)
    //        return ResponseFactory.Fail<bool>("Insufficient balance");

    //    var address = await _context.UserWalletAddresses
    //        .FirstOrDefaultAsync(x => x.UserId == userId);

    //    if (address == null)
    //        return ResponseFactory.Fail<bool>("Wallet address not found");
    //    var pendingAmount = await _context.Withdrawals
    //                    .Where(x =>
    //                        x.UserId == userId &&
    //                        x.Status == DepositStatus.Pending)
    //                    .SumAsync(x => (decimal?)x.Amount) ?? 0;
    //    var availableBalance =wallet.Balance - pendingAmount;
    //    if (availableBalance < request.Amount)
    //    {
    //        return ResponseFactory.Fail<bool>(
    //            "Insufficient available balance");
    //    }
    //    var withdrawal = new Withdrawal
    //    {
    //        UserId = userId,
    //        Amount = request.Amount, 
    //        WalletAddress = address.Address,
    //        Network = address.Network,
    //        Status = DepositStatus.Pending,
    //        CreatedAt =  DateTime.Now
    //    };

    //    _context.Withdrawals.Add(withdrawal);

    //    await _context.SaveChangesAsync();
    //    var adminEmail = await _settingService.GetValueAsync("ADMIN_EMAIL");

    //    var useremail = user.Email;
    //    var requestamount = request.Amount;
    //    var address_address = address.Address;
    //    if (!string.IsNullOrWhiteSpace(adminEmail))
    //    {
    //        _ = Task.Run(async () =>
    //        {
    //            try
    //            {
    //                using var scope = _scopeFactory.CreateScope();

    //                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
    //                var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

    //                await emailService.SendAsync(
    //                    adminEmail,
    //                    "New Withdrawal Request",
    //                    emailTemplateService.GetWithdrawalNotification(
    //                        useremail,
    //                        requestamount,
    //                        address_address)
    //                );

    //            }
    //            catch (Exception ex)
    //            {
    //                //using var scope = _scopeFactory.CreateScope();

    //                //var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

    //                //_context.EmailLogs.Add(new EmailLog
    //                //{
    //                //    ToEmail = useremail,
    //                //    Subject = "New Withdrawal Request",
    //                //    Body = emailTemplateService.GetWithdrawalNotification(
    //                //        useremail,
    //                //        requestamount,
    //                //        address_address),
    //                //    CreatedAt =  DateTime.Now,
    //                //    Status="Faild"
    //                //});
    //                await _context.SaveChangesAsync();
    //                Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
    //            }
    //        });
    //    }
    //    return ResponseFactory.Success(
    //        true,
    //        "Withdrawal request submitted");
    //}
    public async Task<ApiResponse<bool>> SaveWalletAddressAsync(
        string address,
        string network,
        long userId)
    {
        var existing = await _context.UserWalletAddresses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (existing != null)
        {
            existing.Address = address;
            existing.Network = network;
        }
        else
        {
            var walletAddress = new UserWalletAddress
            {
                UserId = userId,
                Address = address,
                Network = network
            };
            _context.UserWalletAddresses.Add(walletAddress);
        }

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "Wallet address saved successfully");
    }
    public async Task<ApiResponse<object>> GetUserProfitsAsync(long userId)
    {
        var ledgers = await _context.ProfitLedgers
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Amount,
                Type = x.Type.ToString(),
                x.ReferenceId,
                x.CreatedAt
            })
            .ToListAsync();

        var totalProfit = ledgers.Sum(x => x.Amount);
        var investmentProfit = ledgers
            .Where(x => x.Type == ProfitType.Investment.ToString())
            .Sum(x => x.Amount);

        var referralProfit = ledgers
            .Where(x => x.Type == ProfitType.Referral.ToString())
            .Sum(x => x.Amount);

        return ResponseFactory.Success<object>(new
        {
            TotalProfit = totalProfit,
            InvestmentProfit = investmentProfit,
            ReferralProfit = referralProfit,
            History = ledgers
        });
    }

    public async Task<ApiResponse<object>> GetReferralOverviewAsync(long userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<object>("User not found");

        var rank = await _context.Ranks
            .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

        var referralWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.WalletType == WalletType.Referral);

        var referrals = await _context.Users
            .Where(x => x.SponsorId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.UserId,
                FullName = x.FirstName + " " + x.LastName,
                x.Email,
                x.ReferralCode,
                x.CreatedAt
            })
            .ToListAsync();

        var bonuses = await _context.ReferralBonusRecords
             .Include(x => x.FromUser)
            .Where(x => x.ToUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.BonusId,
                x.FromUser.Email,
                x.Amount,
                x.Type,
                x.CreatedAt
            })
            .ToListAsync();

        return ResponseFactory.Success<object>(new
        {
            ReferralCode = user.ReferralCode,
            CurrentRank = rank != null ? rank.Name : "-",
            ReferralProfitPercent = rank != null ? rank.ReferralProfitPercent : 0,
            MainToInvestPercent = rank != null ? rank.MainToInvestPercent : 0,
            ReferralWalletBalance = referralWallet != null ? referralWallet.Balance : 0,
            TotalReferrals = referrals.Count,
            TotalBonus = bonuses.Sum(x => x.Amount),
            Referrals = referrals,
            Bonuses = bonuses
        });
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

    public async Task<ApiResponse<List<UserPendingWithdrawalDto>>> GetMyPendingWithdrawalsAsync(long userId)
    {
        var withdrawals = await _context.Withdrawals
            .Where(x =>
                x.UserId == userId &&
                x.Status == DepositStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new UserPendingWithdrawalDto
            {
                WithdrawalId = x.WithdrawalId,
                Amount = x.Amount,
                WalletAddress = x.WalletAddress,
                Network = x.Network,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return ResponseFactory.Success(withdrawals);
    }

    public async Task<ApiResponse<bool>> CancelWithdrawalAsync(long withdrawalId, long userId)
    {
        var withdrawal = await _context.Withdrawals
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

        var before = wallet.Balance;
        var after = before + withdrawal.Amount;

        wallet.Balance = after;

        withdrawal.Status = DepositStatus.Canceled;
        withdrawal.ProcessedAt = DateTime.Now;

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = userId,
            WalletId = wallet.WalletId,
            Amount = withdrawal.Amount,
            BalanceBefore = before,
            BalanceAfter = after,
            Type = TransactionType.Adjustment,
            Description = "Withdrawal request canceled",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ResponseFactory.Success(true, "Withdrawal request canceled");
    }
}