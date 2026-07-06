using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums; 
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class RankService
{
    private readonly OtradeDbContext _context;

    public RankService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task EvaluateAllRanksAsync()
    {
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            await EvaluateUserRank(user);
        }

        await _context.SaveChangesAsync();
    }

    private async Task EvaluateUserRank(User user)
    {
        var volume = await GetNetworkInvestVolume(user.UserId);

        var rank = await _context.Ranks
            .OrderByDescending(x => x.RequiredVolume)
            .FirstOrDefaultAsync(x => x.RequiredVolume <= volume);

        if (rank == null)
            return;

        if (user.CurrentRankId == rank.RankId)
            return;

        var oldRank = user.CurrentRankId;

        user.CurrentRankId = rank.RankId;

        _context.RankHistories.Add(new RankHistory
        {
            UserId = user.UserId,
            OldRankId = oldRank,
            NewRankId = rank.RankId,
            Volume = volume,
            CreatedAt =  DateTime.Now
        });
    }

    // 🔥 مهم‌ترین بخش (اثر کل شبکه)
    private async Task<decimal> GetNetworkInvestVolume(long userId)
    {
        // گرفتن کل پایین‌دستی‌ها
        var descendants = await _context.ReferralRelations
            .Where(x => x.AncestorId == userId)
            .Select(x => x.DescendantId)
            .Distinct()
            .ToListAsync();

        // اضافه کردن خود کاربر
        descendants.Add(userId);

        // جمع کل Invest Wallet ها
        var volume = await _context.Wallets
            .Where(w =>
                descendants.Contains(w.UserId) &&
                w.WalletType == WalletType.Invest)
            .SumAsync(w => (decimal?)w.Balance) ?? 0;

        return volume;
    }
    public async Task<ApiResponse<object>> GetUserRankOverviewAsync(long userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<object>("User not found");

        var volume = await GetNetworkInvestVolume(userId);

        var currentRank = await _context.Ranks
            .FirstOrDefaultAsync(x => x.RankId == user.CurrentRankId);

        var nextRank = await _context.Ranks
            .Where(x => x.RequiredVolume > volume)
            .OrderBy(x => x.RequiredVolume)
            .FirstOrDefaultAsync();

        var allRanks = await _context.Ranks
            .OrderBy(x => x.RequiredVolume)
            .Select(x => new
            {
                x.RankId,
                x.Name,
                x.RequiredVolume,
                x.DailyProfitPercent,
                x.MonthlyProfitPercent,
                x.ReferralProfitPercent,
                x.MainToInvestPercent,
                x.SortOrder
            })
            .ToListAsync();

        decimal progressPercent = 100;

        if (nextRank != null && nextRank.RequiredVolume > 0)
        {
            progressPercent = Math.Round((volume / nextRank.RequiredVolume) * 100, 2);

            if (progressPercent > 100)
                progressPercent = 100;
        }

        return ResponseFactory.Success<object>(new
        {
            NetworkVolume = volume,
            CurrentRank = currentRank == null ? null : new
            {
                currentRank.RankId,
                currentRank.Name,
                currentRank.RequiredVolume,
                currentRank.DailyProfitPercent,
                currentRank.MonthlyProfitPercent,
                currentRank.ReferralProfitPercent,
                currentRank.MainToInvestPercent
            },
            NextRank = nextRank == null ? null : new
            {
                nextRank.RankId,
                nextRank.Name,
                nextRank.RequiredVolume,
                nextRank.DailyProfitPercent,
                nextRank.MonthlyProfitPercent,
                nextRank.ReferralProfitPercent,
                nextRank.MainToInvestPercent
            },
            ProgressPercent = progressPercent,
            RemainingVolume = nextRank == null ? 0 : Math.Max(0, nextRank.RequiredVolume - volume),
            Ranks = allRanks
        });
    }
}