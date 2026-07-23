using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Dashboard;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using Otrade.Application.DTOs.Wallet;
using System.Globalization;
namespace Otrade.Application.Services;

public class DashboardService
{
    private readonly OtradeDbContext _context;

    public DashboardService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<DashboardResponse>> GetDashboardAsync(long userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<DashboardResponse>("User not found");

        var wallets = await _context.Wallets
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var totalAssets = wallets.Sum(x => x.Balance);

        var currentRank = await _context.Ranks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

        currentRank ??= await _context.Ranks
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync();

        var now = DateTime.Now;

        var activeBonusUsages = await _context.BonusCodeUsages
            .AsNoTracking()
            .Include(x => x.AppliedRank)
            .Where(x =>
                x.UserId == userId &&
                x.Status == BonusCodeUsageStatus.Active &&
                (
                    x.ExpiresAt == null ||
                    x.ExpiresAt >= now
                ))
            .ToListAsync();

        var activeBonusCapital = activeBonusUsages
            .Sum(x => x.BonusCapitalAmount);

        var bestBonusRankUsage = activeBonusUsages
            .Where(x => x.AppliedRank != null)
            .OrderByDescending(x => x.AppliedRank!.SortOrder)
            .FirstOrDefault();

        var hasActiveBonusRank =
            bestBonusRankUsage?.AppliedRank != null &&
            (
                currentRank == null ||
                bestBonusRankUsage.AppliedRank.SortOrder > currentRank.SortOrder
            );

        var effectiveRank = hasActiveBonusRank
            ? bestBonusRankUsage!.AppliedRank!
            : currentRank;

        var investWalletBalance = wallets
            .Where(x => x.WalletType == WalletType.Invest)
            .Sum(x => x.Balance);

        var investProfitBase = investWalletBalance + activeBonusCapital;

        var bonusCapitalExpiresAt = activeBonusUsages
            .Where(x =>
                x.BonusCapitalAmount > 0 &&
                x.ExpiresAt.HasValue)
            .OrderBy(x => x.ExpiresAt)
            .Select(x => x.ExpiresAt)
            .FirstOrDefault();

        var networkVolume = await GetNetworkInvestVolumeAsync(userId);

        var nextRank = await _context.Ranks
            .Where(x => x.RequiredVolume > networkVolume)
            .OrderBy(x => x.RequiredVolume)
            .FirstOrDefaultAsync();

        decimal requiredForNext = 0;
        decimal progressPercent = 100;

        if (nextRank != null)
        {
            requiredForNext = nextRank.RequiredVolume - networkVolume;

            if (nextRank.RequiredVolume > 0)
            {
                progressPercent =
                    Math.Round((networkVolume / nextRank.RequiredVolume) * 100, 2);

                if (progressPercent > 100)
                    progressPercent = 100;
            }
        }
        var currentInvestmentCapacity = await GetCurrentInvestmentCapacityAsync();
        var response = new DashboardResponse
        {
            UserUid = user.ReferralCode,
            TotalAssets = totalAssets,
            BaseRank = currentRank?.Name ?? "Basic",
            EffectiveRank = effectiveRank?.Name ?? currentRank?.Name ?? "Basic",
            CurrentRank = effectiveRank?.Name ?? currentRank?.Name ?? "Basic",

            HasActiveBonusRank = hasActiveBonusRank,
            BonusRankName = hasActiveBonusRank
        ? bestBonusRankUsage?.AppliedRank?.Name
        : null,
            BonusRankExpiresAt = hasActiveBonusRank
        ? bestBonusRankUsage?.ExpiresAt
        : null,

            ActiveBonusCapital = activeBonusCapital,
            BonusCapitalExpiresAt = bonusCapitalExpiresAt,
            ActiveBonusCount = activeBonusUsages.Count,
            InvestProfitBase = investProfitBase,

            NetworkVolume = networkVolume,
            NextRank = nextRank?.Name,
            RequiredForNextRank = requiredForNext,
            NextRankProgressPercent = progressPercent,
            CurrentInvestmentCapacity = currentInvestmentCapacity,
            Wallets = wallets.Select(w => new WalletBalanceDto
            {
                WalletId = w.WalletId,
                WalletType = w.WalletType.ToString(),
                Balance = w.Balance,
                PercentOfTotal = totalAssets > 0
                    ? Math.Round((w.Balance / totalAssets) * 100, 2)
                    : 0
            }).ToList()
        };

        return ResponseFactory.Success(response);
    }

    private async Task<decimal> GetNetworkInvestVolumeAsync(long userId)
    {
        var descendantIds = await _context.ReferralRelations
            .Where(x => x.AncestorId == userId)
            .Select(x => x.DescendantId)
            .ToListAsync();

        descendantIds.Add(userId);

        return await _context.Wallets
            .Where(x =>
                descendantIds.Contains(x.UserId) &&
                x.WalletType == WalletType.Invest)
            .SumAsync(x => (decimal?)x.Balance) ?? 0;
    }
    private async Task<CurrentInvestmentCapacityResponse> GetCurrentInvestmentCapacityAsync()
    {
        var now = DateTime.Now;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1);

        var capacity = await _context.InvestmentCapacities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonthStart == monthStart);

        if (capacity == null)
        {
            return new CurrentInvestmentCapacityResponse
            {
                IsConfigured = false,
                IsActive = false,
                Year = monthStart.Year,
                Month = monthStart.Month,
                MonthLabel = monthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
                RemainingCapacity = 0
            };
        }

        var remaining = capacity.TotalCapacity - capacity.UsedCapacity;

        if (remaining < 0)
            remaining = 0;

        return new CurrentInvestmentCapacityResponse
        {
            IsConfigured = true,
            IsActive = capacity.IsActive,
            Year = capacity.MonthStart.Year,
            Month = capacity.MonthStart.Month,
            MonthLabel = capacity.MonthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
            RemainingCapacity = remaining
        };
    }
}