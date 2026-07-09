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
            .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

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
            TotalAssets = totalAssets,
            CurrentRank = currentRank?.Name ?? "Basic",
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