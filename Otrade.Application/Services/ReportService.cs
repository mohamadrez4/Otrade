using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Reports;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class ReportService
{
    private readonly OtradeDbContext _context;

    public ReportService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<UserReportResponse>> GetUserReportAsync(long userId)
    {
        var deposits = await _context.Deposits
            .Where(x => x.UserId == userId )
            .Select(x => new DepositDto
            {
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var withdrawals = await _context.Withdrawals
            .Where(x => x.UserId == userId)
            .Select(x => new WithdrawalDto
            {
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var transfers = await _context.WalletTransactions
            .Where(x => x.UserId == userId)
            .Select(x => new WalletTransactionDto
            {
                WalletType = x.Wallet.WalletType.ToString(),
                Amount = x.Amount,
                Type = x.Type.ToString(),
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var investmentProfits = await _context.ProfitLedgers
            .Where(x => x.UserId == userId && x.Type == ProfitType.Investment)
            .Select(x => new ProfitDto
            {
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var referralProfits = await _context.ProfitLedgers
            .Where(x => x.UserId == userId && x.Type == ProfitType.Referral)
            .Select(x => new ProfitDto
            {
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var mainInvestBonuses = await _context.ReferralBonusRecords
            .Where(x => x.ToUserId == userId && x.Type == "MainToInvestBonus")
            .Select(x => new BonusDto
            {
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var response = new UserReportResponse
        {
            Deposits = deposits,
            Withdrawals = withdrawals,
            Transfers = transfers,
            InvestmentProfits = investmentProfits,
            ReferralProfits = referralProfits,
            MainInvestBonuses = mainInvestBonuses
        };

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<AdminReportResponse>> GetAdminReportAsync()
    {
        var totalDeposits = await _context.Deposits
            .Where(x => x.Status == DepositStatus.Approved)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalWithdrawals = await _context.Withdrawals
            .Where(x => x.Status == DepositStatus.Approved)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalInvestmentProfits = await _context.ProfitLedgers
            .Where(x => x.Type == ProfitType.Investment)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalReferralProfits = await _context.ProfitLedgers
            .Where(x => x.Type == ProfitType.Referral)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalMainInvestBonuses = await _context.ReferralBonusRecords
            .Where(x => x.Type == "MainToInvestBonus")
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var pendingDepositsCount = await _context.Deposits
            .CountAsync(x => x.Status == DepositStatus.Pending);

        var pendingWithdrawalsCount = await _context.Withdrawals
            .CountAsync(x => x.Status == DepositStatus.Pending);

        var totalUsers = await _context.Users.CountAsync();

        var response = new AdminReportResponse
        {
            TotalDeposits = totalDeposits,
            TotalWithdrawals = totalWithdrawals,
            TotalInvestmentProfits = totalInvestmentProfits,
            TotalReferralProfits = totalReferralProfits,
            TotalMainInvestBonuses = totalMainInvestBonuses,
            PendingDepositsCount = pendingDepositsCount,
            PendingWithdrawalsCount = pendingWithdrawalsCount,
            TotalUsers = totalUsers
        };

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<AdminDetailReportResponse>> GetAdminDetailReportAsync()
    {
        var deposits = await _context.Deposits
            .Include(x => x.User)
            .Select(x => new AdminDepositDto
            {
                UserEmail = x.User.Email,
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var withdrawals = await _context.Withdrawals
            .Include(x => x.User)
            .Select(x => new AdminWithdrawalDto
            {
                UserEmail = x.User.Email,
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var transfers = await _context.WalletTransactions
            .Include(x => x.User)
            .Include(x => x.Wallet)
            .Select(x => new AdminWalletTransactionDto
            {
                UserEmail = x.User.Email,
                WalletType = x.Wallet.WalletType.ToString(),
                Amount = x.Amount,
                Type = x.Type.ToString(),
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var investmentProfits = await _context.ProfitLedgers
            .Include(x => x.User)
            .Where(x => x.Type == ProfitType.Investment)
            .Select(x => new AdminProfitDto
            {
                UserEmail = x.User.Email,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var referralProfits = await _context.ProfitLedgers
            .Include(x => x.User)
            .Where(x => x.Type == ProfitType.Referral)
            .Select(x => new AdminProfitDto
            {
                UserEmail = x.User.Email,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var mainInvestBonuses = await _context.ReferralBonusRecords
            .Include(x => x.FromUser)
            .Include(x => x.ToUser)
            .Where(x => x.Type == "MainToInvestBonus")
            .Select(x => new AdminBonusDto
            {
                FromUserEmail = x.FromUser.Email,
                ToUserEmail = x.ToUser.Email,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var response = new AdminDetailReportResponse
        {
            Deposits = deposits,
            Withdrawals = withdrawals,
            Transfers = transfers,
            InvestmentProfits = investmentProfits,
            ReferralProfits = referralProfits,
            MainInvestBonuses = mainInvestBonuses
        };

        return ResponseFactory.Success(response);
    }
}