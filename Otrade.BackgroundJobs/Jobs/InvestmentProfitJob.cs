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
    private readonly JobLockservice _lockService;

    public InvestmentProfitJob(
        IServiceScopeFactory scopeFactory,
        ReferralProfitService referralProfitService,
        JobLockservice lockService)
    {
        _scopeFactory = scopeFactory;
        _referralProfitService = referralProfitService;
        _lockService = lockService;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.Now;
        var today = now.DayOfWeek;
        


        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<OtradeDbContext>();
        await ExpireBonusCodeUsagesAsync(context, now);

        if (today == DayOfWeek.Saturday || today == DayOfWeek.Sunday)
            return;
        var locked = await _lockService.TryAcquireLockAsync("InvestmentProfitJob");

        if (!locked)
            return;

        var users = await context.Users
            .AsNoTracking()
            .Where(x => x.CurrentRankId != null)
            .ToListAsync();

        foreach (var user in users)
        {
            var investWallet = await context.Wallets
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.UserId &&
                    x.WalletType == WalletType.Invest);

            var profitWallet = await context.Wallets
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.UserId &&
                    x.WalletType == WalletType.Profit);

            var currentRank = await context.Ranks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

            var contract = await context.Contracts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.UserId &&
                    x.Status == ContractStatus.Active);

            if (contract == null)
                continue;

            if (investWallet == null || profitWallet == null || currentRank == null)
                continue;

            var todayKey = $"INVEST_{user.UserId}_{now:yyyyMMdd}";

            var alreadyPaidToday = await context.ProfitLedgers
                .AnyAsync(x => x.ReferenceId == todayKey);

            if (alreadyPaidToday)
                continue;

            var activeBonusUsages = await context.BonusCodeUsages
                .AsNoTracking()
                .Include(x => x.AppliedRank)
                .Where(x =>
                    x.UserId == user.UserId &&
                    x.Status == BonusCodeUsageStatus.Active &&
                    (
                        x.ExpiresAt == null ||
                        x.ExpiresAt >= now
                    ))
                .ToListAsync();

            var activeBonusCapital = activeBonusUsages
                .Sum(x => x.BonusCapitalAmount);

            var effectiveRank = GetEffectiveRank(
                currentRank,
                activeBonusUsages);

            var realCapital = investWallet.Balance;
            var profitBase = realCapital + activeBonusCapital;

            if (profitBase <= 0)
                continue;

            var profit = profitBase * (effectiveRank.DailyProfitPercent / 100m);

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
                Description =
                    $"Daily Investment Profit | Real Capital: {realCapital:F2} USDT | " +
                    $"Bonus Capital: {activeBonusCapital:F2} USDT | " +
                    $"Effective Rank: {effectiveRank.Name}",
                CreatedAt = now
            });

            context.ProfitLedgers.Add(new ProfitLedger
            {
                UserId = user.UserId,
                ReferenceId = todayKey,
                Amount = profit,
                Type = ProfitType.Investment,
                CreatedAt = now
            });

            await _referralProfitService.HandleReferralProfit(
                user.UserId,
                profit);
        }

        await context.SaveChangesAsync();
    }

    private static Rank GetEffectiveRank(
        Rank currentRank,
        List<BonusCodeUsage> activeBonusUsages)
    {
        var bestBonusRank = activeBonusUsages
            .Where(x => x.AppliedRank != null)
            .Select(x => x.AppliedRank!)
            .OrderByDescending(x => x.SortOrder)
            .FirstOrDefault();

        if (bestBonusRank == null)
            return currentRank;

        if (bestBonusRank.SortOrder > currentRank.SortOrder)
            return bestBonusRank;

        return currentRank;
    }

    private static async Task ExpireBonusCodeUsagesAsync(
        OtradeDbContext context,
        DateTime now)
    {
        var expiredUsages = await context.BonusCodeUsages
            .Where(x =>
                x.Status == BonusCodeUsageStatus.Active &&
                x.ExpiresAt != null &&
                x.ExpiresAt < now)
            .ToListAsync();

        if (!expiredUsages.Any())
            return;

        foreach (var usage in expiredUsages)
        {
            usage.Status = BonusCodeUsageStatus.Expired;
            usage.CompletedAt = now;
            usage.AdminNote = "Expired automatically by InvestmentProfitJob.";
        }

        await context.SaveChangesAsync();
    }
}