using Microsoft.EntityFrameworkCore;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class MainInvestBonusService
{
    private readonly OtradeDbContext _context;

    public MainInvestBonusService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task HandleBonusAsync(
        long userId,
        decimal investAmount)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null || user.SponsorId == null)
            return;

        var sponsor = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == user.SponsorId);

        if (sponsor == null)
            return;

        var rank = await _context.Ranks
            .FirstOrDefaultAsync(x => x.RankId == sponsor.CurrentRankId);

        if (rank == null)
            return;

        var bonus = investAmount *
                    (rank.MainToInvestPercent / 100);

        if (bonus <= 0)
            return;

        var referralWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == sponsor.UserId &&
                x.WalletType == WalletType.Referral);

        if (referralWallet == null)
            return;

        var before = referralWallet.Balance;
        var after = before + bonus;

        referralWallet.Balance = after;

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = sponsor.UserId,
            WalletId = referralWallet.WalletId,
            Amount = bonus,
            BalanceBefore = before,
            BalanceAfter = after,
            Type = TransactionType.ReferralBonus,
            Description = $"Referral Commission from User #{userId}",
            CreatedAt =  DateTime.Now
        });

        _context.ReferralBonusRecords.Add(
            new ReferralBonusRecord
            {
                FromUserId = userId,
                ToUserId = sponsor.UserId,
                Amount = bonus,
                Type = "MainToInvestBonus",
                CreatedAt =  DateTime.Now
            });
    }
}