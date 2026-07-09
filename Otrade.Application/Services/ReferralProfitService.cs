using Microsoft.EntityFrameworkCore;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class ReferralProfitService
{
    private readonly OtradeDbContext _context;

    public ReferralProfitService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task HandleReferralProfit(long fromUserId, decimal childProfit)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == fromUserId);

        if (user == null || user.SponsorId == null)
            return;

        var sponsor = await _context.Users.FirstOrDefaultAsync(x => x.UserId == user.SponsorId);

        if (sponsor == null)
            return;

        var rank = await _context.Ranks.FirstOrDefaultAsync(x =>
            x.RankId == sponsor.CurrentRankId);

        if (rank == null)
            return;

        var bonus = childProfit * (rank.ReferralProfitPercent / 100);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(x =>
            x.UserId == sponsor.UserId &&
            x.WalletType == WalletType.Referral);

        if (wallet == null)
            return;

        // 🔥 جلوگیری از دوبار پرداخت
        var reference = $"REF_{fromUserId}_{ DateTime.Now:yyyyMMddHH}";

        var exists = await _context.ProfitLedgers
            .AnyAsync(x => x.ReferenceId == reference);

        if (exists)
            return;

        var before = wallet.Balance;
        var after = before + bonus;

        wallet.Balance = after;

        _context.WalletTransactions.Add(new WalletTransaction
        {
            UserId = sponsor.UserId,
            WalletId = wallet.WalletId,
            Amount = bonus,
            BalanceBefore = before,
            BalanceAfter = after,
            Type = TransactionType.ReferralBonus,
            Description = $"Referral Profit from UID {user.ReferralCode}",
            CreatedAt = DateTime.Now
        });

        _context.ProfitLedgers.Add(new ProfitLedger
        {
            UserId = sponsor.UserId,
            SourceUserId = fromUserId,
            ReferenceId = reference,
            Amount = bonus,
            Type = ProfitType.Referral,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }
}