using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Dashboard;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

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

        var response = new DashboardResponse
        {
            TotalAssets = totalAssets,
            CurrentRank = currentRank?.Name ?? "Basic",
            NetworkVolume = networkVolume,
            NextRank = nextRank?.Name,
            RequiredForNextRank = requiredForNext,
            NextRankProgressPercent = progressPercent,
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
}