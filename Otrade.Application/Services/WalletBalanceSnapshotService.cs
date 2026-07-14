using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.DTOs.Common;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class WalletBalanceSnapshotService
{
    private readonly OtradeDbContext _context;

    public WalletBalanceSnapshotService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<WalletBalanceSnapshotDto>> CreateOrUpdateTodaySnapshotAsync()
    {
        var snapshotDate = DateTime.Now.Date;
        var now = DateTime.Now;

        var summary = await CalculateCurrentSummaryAsync();

        var snapshot = await _context.WalletBalanceSnapshots
            .FirstOrDefaultAsync(x => x.SnapshotDate == snapshotDate);

        if (snapshot == null)
        {
            snapshot = new WalletBalanceSnapshot
            {
                SnapshotDate = snapshotDate,
                CreatedAt = now
            };

            _context.WalletBalanceSnapshots.Add(snapshot);
        }
        else
        {
            snapshot.UpdatedAt = now;
        }

        snapshot.TotalMainWallet = summary.TotalMainWallet;
        snapshot.TotalInvestWallet = summary.TotalInvestWallet;
        snapshot.TotalProfitWallet = summary.TotalProfitWallet;
        snapshot.TotalReferralWallet = summary.TotalReferralWallet;
        snapshot.TotalAssets = summary.TotalAssets;
        snapshot.TotalWallets = summary.TotalWallets;
        snapshot.UsersWithBalance = summary.UsersWithBalance;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            MapToDto(snapshot),
            "Wallet balance snapshot saved successfully");
    }

    public async Task<ApiResponse<PagedResponse<WalletBalanceSnapshotDto>>> GetSnapshotsAsync(
        int page,
        int pageSize,
        DateTime? fromDate,
        DateTime? toDate)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var query = _context.WalletBalanceSnapshots
            .AsNoTracking()
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.SnapshotDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(x => x.SnapshotDate <= to);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.SnapshotDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletBalanceSnapshotDto
            {
                SnapshotId = x.SnapshotId,
                SnapshotDate = x.SnapshotDate,
                TotalMainWallet = x.TotalMainWallet,
                TotalInvestWallet = x.TotalInvestWallet,
                TotalProfitWallet = x.TotalProfitWallet,
                TotalReferralWallet = x.TotalReferralWallet,
                TotalAssets = x.TotalAssets,
                TotalWallets = x.TotalWallets,
                UsersWithBalance = x.UsersWithBalance,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        var response = new PagedResponse<WalletBalanceSnapshotDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };

        return ResponseFactory.Success(response);
    }

    private async Task<WalletBalanceSnapshotDto> CalculateCurrentSummaryAsync()
    {
        var groupedWallets = await _context.Wallets
            .AsNoTracking()
            .GroupBy(x => x.WalletType)
            .Select(x => new
            {
                WalletType = x.Key,
                Total = x.Sum(w => w.Balance)
            })
            .ToListAsync();

        decimal GetTotal(WalletType walletType)
        {
            return groupedWallets
                .FirstOrDefault(x => x.WalletType == walletType)
                ?.Total ?? 0;
        }

        var totalMain = GetTotal(WalletType.Main);
        var totalInvest = GetTotal(WalletType.Invest);
        var totalProfit = GetTotal(WalletType.Profit);
        var totalReferral = GetTotal(WalletType.Referral);

        var totalWallets = await _context.Wallets
            .AsNoTracking()
            .CountAsync();

        var usersWithBalance = await _context.Wallets
            .AsNoTracking()
            .Where(x => x.Balance > 0)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync();

        return new WalletBalanceSnapshotDto
        {
            TotalMainWallet = totalMain,
            TotalInvestWallet = totalInvest,
            TotalProfitWallet = totalProfit,
            TotalReferralWallet = totalReferral,
            TotalAssets = totalMain + totalInvest + totalProfit + totalReferral,
            TotalWallets = totalWallets,
            UsersWithBalance = usersWithBalance
        };
    }

    private static WalletBalanceSnapshotDto MapToDto(WalletBalanceSnapshot snapshot)
    {
        return new WalletBalanceSnapshotDto
        {
            SnapshotId = snapshot.SnapshotId,
            SnapshotDate = snapshot.SnapshotDate,
            TotalMainWallet = snapshot.TotalMainWallet,
            TotalInvestWallet = snapshot.TotalInvestWallet,
            TotalProfitWallet = snapshot.TotalProfitWallet,
            TotalReferralWallet = snapshot.TotalReferralWallet,
            TotalAssets = snapshot.TotalAssets,
            TotalWallets = snapshot.TotalWallets,
            UsersWithBalance = snapshot.UsersWithBalance,
            CreatedAt = snapshot.CreatedAt,
            UpdatedAt = snapshot.UpdatedAt
        };
    }
}