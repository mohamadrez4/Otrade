using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common.Locks;
using Otrade.Application.Services;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.BackgroundJobs.Jobs;
public class InvestmentProfitJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReferralProfitService _referralProfitService;
    private readonly JobLockService _lockService;
    public InvestmentProfitJob(IServiceScopeFactory scopeFactory, ReferralProfitService referralProfitService,JobLockService lockService)
    {
        _scopeFactory = scopeFactory;
        _referralProfitService = referralProfitService;
        _lockService = lockService;
    }

    public async Task ExecuteAsync()
    {
        var today =  DateTime.Now.DayOfWeek;

        // Saturday = Saturday
        // Sunday = Sunday

        if (today == DayOfWeek.Saturday || today == DayOfWeek.Sunday)
        {
            return; // No profit on weekends
        }
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<OtradeDbContext>();
        var locked = await _lockService.TryAcquireLockAsync("InvestmentProfitJob");

        if (!locked)
            return; // ❌ Job already running
        var users = await context.Users.ToListAsync();

        foreach (var user in users)
        {
            var investWallet = await context.Wallets.FirstOrDefaultAsync(x =>
                x.UserId == user.UserId &&
                x.WalletType == WalletType.Invest);

            var profitWallet = await context.Wallets.FirstOrDefaultAsync(x =>
                x.UserId == user.UserId &&
                x.WalletType == WalletType.Profit);

            var rank = await context.Ranks.FirstOrDefaultAsync(x =>
                x.RankId == user.CurrentRankId);
            var contract = await context.Contracts.FirstOrDefaultAsync(x =>
                x.UserId == user.UserId &&
                x.Status == ContractStatus.Active);

            if (contract == null)
                continue;

            if (investWallet == null || profitWallet == null || rank == null)
                continue;

            // 🔥 جلوگیری از double profit (امروز فقط یکبار)
            var todayKey = $"INVEST_{user.UserId}_{ DateTime.Now:yyyyMMdd}";

            var exists = await context.ProfitLedgers
                .AnyAsync(x => x.ReferenceId == todayKey);

            if (exists)
                continue;

            var profit = investWallet.Balance * (rank.DailyProfitPercent / 100);

            if (profit <= 0)
                continue;

            var before = profitWallet.Balance;
            var after = before + profit;

            profitWallet.Balance = after;

            context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = user.UserId,
                WalletId = profitWallet.WalletId,
                Amount = profit,
                BalanceBefore = before,
                BalanceAfter = after,
                Type = TransactionType.Profit,
                Description = "Daily Investment Profit",
                CreatedAt =  DateTime.Now
            });
            await _referralProfitService.HandleReferralProfit(user.UserId, profit); 
            context.ProfitLedgers.Add(new ProfitLedger
            {
                UserId = user.UserId,
                ReferenceId = todayKey,
                Amount = profit,
                Type = ProfitType.Investment,
                CreatedAt =  DateTime.Now
            });
        }

        await context.SaveChangesAsync();
    }
}