using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Common;
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
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DepositDto
            {
                DepositId = x.DepositId,
                Amount = x.Amount,
                TxId = x.TxId,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var withdrawals = await _context.Withdrawals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WithdrawalDto
            {
                WithdrawalId = x.WithdrawalId,
                Amount = x.Amount,
                WalletAddress = x.WalletAddress,
                Network = x.Network,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var transferRows = await _context.WalletTransfers
            .AsNoTracking()
            .Include(x => x.FromWallet)
                .ThenInclude(x => x.User)
            .Include(x => x.ToWallet)
                .ThenInclude(x => x.User)
            .Where(x =>
                x.FromWallet.UserId == userId ||
                x.ToWallet.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.TransferId,
                x.Amount,
                x.Description,
                x.CreatedAt,

                FromWalletType = x.FromWallet.WalletType,
                ToWalletType = x.ToWallet.WalletType,

                FromUserId = x.FromWallet.UserId,
                FromEmail = x.FromWallet.User.Email,
                FromUid = x.FromWallet.User.ReferralCode,

                ToUserId = x.ToWallet.UserId,
                ToEmail = x.ToWallet.User.Email,
                ToUid = x.ToWallet.User.ReferralCode
            })
            .ToListAsync();

        var transfers = transferRows
            .Select(x => new WalletTransactionDto
            {
                TransferId = x.TransferId,

                Direction = x.FromUserId == x.ToUserId
                    ? "Wallet Transfer"
                    : x.FromUserId == userId
                        ? "Sent"
                        : "Received",

                FromWalletType = x.FromWalletType.ToString(),
                ToWalletType = x.ToWalletType.ToString(),

                FromEmail = x.FromEmail,
                FromUid = x.FromUid,

                ToEmail = x.ToEmail,
                ToUid = x.ToUid,

                Amount = x.Amount,
                Description = x.Description,
                CreatedAt = x.CreatedAt
            })
            .ToList();
        var investmentProfits = await _context.ProfitLedgers
            .Where(x => x.UserId == userId && x.Type == ProfitType.Investment)
            .Select(x => new ProfitDto
            {
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            }).ToListAsync();

        var referralProfits = await _context.ProfitLedgers
            .AsNoTracking()
            .Include(x => x.SourceUser)
            .Where(x => x.UserId == userId && x.Type == ProfitType.Referral)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProfitDto
            {
                Amount = x.Amount,
                CreatedAt = x.CreatedAt,

                SourceUserUid = x.SourceUser != null
                    ? x.SourceUser.ReferralCode
                    : null,

                SourceUserEmail = x.SourceUser != null
                    ? x.SourceUser.Email
                    : null,

                SourceUserFullName = x.SourceUser != null
                    ? (x.SourceUser.FirstName + " " + x.SourceUser.LastName).Trim()
                    : null
            })
            .ToListAsync();

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
        var pendingPreRegistrationsCount = await _context.TemporaryRegistrations
            .CountAsync(x => x.Status == TemporaryRegistrationStatus.DepositSubmitted);
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
            PendingPreRegistrationsCount = pendingPreRegistrationsCount,
            TotalUsers = totalUsers
        };

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<AdminDetailReportResponse>> GetAdminDetailReportAsync(
     AdminReportFilterRequest filter)
    {
        filter ??= new AdminReportFilterRequest();

        var email = filter.Email?.Trim().ToLowerInvariant();
        var statusText = filter.Status?.Trim();
        var fromDate = filter.FromDate?.Date;
        var toDateExclusive = filter.ToDate?.Date.AddDays(1);
        var minAmount = filter.MinAmount;
        var maxAmount = filter.MaxAmount;

        var hasStatusFilter =
            !string.IsNullOrWhiteSpace(statusText) &&
            !statusText.Equals("All", StringComparison.OrdinalIgnoreCase);

        DepositStatus parsedStatus = DepositStatus.Pending;
        KycStatus parsedKycStatus = KycStatus.Pending;
        var statusParsed = false;
        var kycStatusParsed = false;
        if (hasStatusFilter)
        {
            statusParsed = Enum.TryParse(statusText, true, out parsedStatus);
            kycStatusParsed = Enum.TryParse(statusText, true, out parsedKycStatus);
        }

        var depositsQuery = _context.Deposits
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            depositsQuery = depositsQuery.Where(x =>
                x.User.Email.ToLower().Contains(email) ||
                x.User.ReferralCode.ToLower().Contains(email) ||
                x.User.FirstName.ToLower().Contains(email) ||
                x.User.LastName.ToLower().Contains(email) ||
                x.TxId.ToLower().Contains(email));
        }

        if (fromDate.HasValue)
            depositsQuery = depositsQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            depositsQuery = depositsQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            depositsQuery = depositsQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            depositsQuery = depositsQuery.Where(x => x.Amount <= maxAmount.Value);

        if (hasStatusFilter && statusParsed)
            depositsQuery = depositsQuery.Where(x => x.Status == parsedStatus);

        var deposits = await depositsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminDepositDto
            {
                DepositId = x.DepositId,

                UserEmail = x.User.Email,
                UserUid = x.User.ReferralCode,
                UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),

                Amount = x.Amount,
                TxId = x.TxId,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var withdrawalsQuery = _context.Withdrawals
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            withdrawalsQuery = withdrawalsQuery.Where(x =>
                x.User.Email.ToLower().Contains(email) ||
                x.User.ReferralCode.ToLower().Contains(email) ||
                x.User.FirstName.ToLower().Contains(email) ||
                x.User.LastName.ToLower().Contains(email) ||
                x.WalletAddress.ToLower().Contains(email) ||
                x.Network.ToLower().Contains(email));
        }

        if (fromDate.HasValue)
            withdrawalsQuery = withdrawalsQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            withdrawalsQuery = withdrawalsQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            withdrawalsQuery = withdrawalsQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            withdrawalsQuery = withdrawalsQuery.Where(x => x.Amount <= maxAmount.Value);

        if (hasStatusFilter && statusParsed)
            withdrawalsQuery = withdrawalsQuery.Where(x => x.Status == parsedStatus);

        var withdrawals = await withdrawalsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminWithdrawalDto
            {
                WithdrawalId = x.WithdrawalId,

                UserEmail = x.User.Email,
                UserUid = x.User.ReferralCode,
                UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),

                Amount = x.Amount,
                WalletAddress = x.WalletAddress,
                Network = x.Network,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();

        var kycsQuery = _context.KycDocuments
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            kycsQuery = kycsQuery.Where(x =>
                x.User.Email.ToLower().Contains(email) ||
                x.User.ReferralCode.ToLower().Contains(email) ||
                x.User.FirstName.ToLower().Contains(email) ||
                x.User.LastName.ToLower().Contains(email) ||
                x.DocumentType.ToString().ToLower().Contains(email));
        }

        if (fromDate.HasValue)
            kycsQuery = kycsQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            kycsQuery = kycsQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (hasStatusFilter && kycStatusParsed)
            kycsQuery = kycsQuery.Where(x => x.Status == parsedKycStatus);

        var kycsRaw = await kycsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.DocumentId,
                x.UserId,

                UserEmail = x.User.Email,
                UserUid = x.User.ReferralCode,
                UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),

                DocumentType = x.DocumentType.ToString(),
                Status = x.Status.ToString(),
                x.RejectReason,
                x.CreatedAt,
                x.ReviewedAt,
                x.ReviewedByAdminId
            })
            .ToListAsync();

        var reviewerIds = kycsRaw
            .Where(x => x.ReviewedByAdminId.HasValue)
            .Select(x => x.ReviewedByAdminId!.Value)
            .Distinct()
            .ToList();

        var reviewers = await _context.Users
            .AsNoTracking()
            .Where(x => reviewerIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                x.Email,
                x.ReferralCode
            })
            .ToDictionaryAsync(x => x.UserId);

        var kycs = kycsRaw
            .Select(x =>
            {
                reviewers.TryGetValue(
                    x.ReviewedByAdminId ?? 0,
                    out var reviewer);

                return new AdminKycReportDto
                {
                    DocumentId = x.DocumentId,
                    UserId = x.UserId,

                    UserEmail = x.UserEmail,
                    UserUid = x.UserUid,
                    UserFullName = x.UserFullName,

                    DocumentType = x.DocumentType,
                    Status = x.Status,
                    RejectReason = x.RejectReason,

                    CreatedAt = x.CreatedAt,
                    ReviewedAt = x.ReviewedAt,

                    ReviewedByAdminId = x.ReviewedByAdminId,
                    ReviewedByAdminEmail = reviewer?.Email,
                    ReviewedByAdminUid = reviewer?.ReferralCode
                };
            })
            .ToList();
        var transfersQuery = _context.WalletTransfers
      .AsNoTracking()
      .Include(x => x.FromWallet)
          .ThenInclude(x => x.User)
      .Include(x => x.ToWallet)
          .ThenInclude(x => x.User)
      .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            transfersQuery = transfersQuery.Where(x =>
                x.FromWallet.User.Email.ToLower().Contains(email) ||
                x.ToWallet.User.Email.ToLower().Contains(email) ||
                x.FromWallet.User.ReferralCode.ToLower().Contains(email) ||
                x.ToWallet.User.ReferralCode.ToLower().Contains(email));
        }

        if (fromDate.HasValue)
            transfersQuery = transfersQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            transfersQuery = transfersQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            transfersQuery = transfersQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            transfersQuery = transfersQuery.Where(x => x.Amount <= maxAmount.Value);

        var transferRows = await transfersQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.TransferId,
                x.Amount,
                x.Description,
                x.CreatedAt,

                FromWalletType = x.FromWallet.WalletType,
                ToWalletType = x.ToWallet.WalletType,

                FromUserEmail = x.FromWallet.User.Email,
                FromUserUid = x.FromWallet.User.ReferralCode,

                ToUserEmail = x.ToWallet.User.Email,
                ToUserUid = x.ToWallet.User.ReferralCode
            })
            .ToListAsync();

        var transfers = transferRows
            .Select(x => new AdminWalletTransactionDto
            {
                TransferId = x.TransferId,

                FromUserEmail = x.FromUserEmail,
                FromUserUid = x.FromUserUid,

                ToUserEmail = x.ToUserEmail,
                ToUserUid = x.ToUserUid,

                FromWalletType = x.FromWalletType.ToString(),
                ToWalletType = x.ToWalletType.ToString(),

                Amount = x.Amount,
                Description = x.Description,
                CreatedAt = x.CreatedAt
            })
            .ToList();
        var investmentProfitsQuery = _context.ProfitLedgers
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.Type == ProfitType.Investment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
            investmentProfitsQuery = investmentProfitsQuery.Where(x => x.User.Email.ToLower().Contains(email));

        if (fromDate.HasValue)
            investmentProfitsQuery = investmentProfitsQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            investmentProfitsQuery = investmentProfitsQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            investmentProfitsQuery = investmentProfitsQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            investmentProfitsQuery = investmentProfitsQuery.Where(x => x.Amount <= maxAmount.Value);

        var investmentProfits = await investmentProfitsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminProfitDto
            {
                UserEmail = x.User.Email,
                UserUid = x.User.ReferralCode,
                ProfitType = "Investment Profit",
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var referralProfitsQuery = _context.ProfitLedgers
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.SourceUser)
            .Where(x => x.Type == ProfitType.Referral)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            referralProfitsQuery = referralProfitsQuery.Where(x =>
                x.User.Email.ToLower().Contains(email) ||
                x.User.ReferralCode.ToLower().Contains(email) ||
                (x.SourceUser != null && x.SourceUser.Email.ToLower().Contains(email)) ||
                (x.SourceUser != null && x.SourceUser.ReferralCode.ToLower().Contains(email)) ||
                (x.SourceUser != null && x.SourceUser.FirstName.ToLower().Contains(email)) ||
                (x.SourceUser != null && x.SourceUser.LastName.ToLower().Contains(email)));
        }

        if (fromDate.HasValue)
            referralProfitsQuery = referralProfitsQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            referralProfitsQuery = referralProfitsQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            referralProfitsQuery = referralProfitsQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            referralProfitsQuery = referralProfitsQuery.Where(x => x.Amount <= maxAmount.Value);

        var referralProfits = await referralProfitsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminProfitDto
            {
                UserEmail = x.User.Email,
                UserUid = x.User.ReferralCode,

                SourceUserEmail = x.SourceUser != null
                    ? x.SourceUser.Email
                    : null,

                SourceUserUid = x.SourceUser != null
                    ? x.SourceUser.ReferralCode
                    : null,

                SourceUserFullName = x.SourceUser != null
                    ? (x.SourceUser.FirstName + " " + x.SourceUser.LastName).Trim()
                    : null,

                ProfitType = "Referral Profit",
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
        var mainInvestBonusesQuery = _context.ReferralBonusRecords
            .AsNoTracking()
            .Include(x => x.FromUser)
            .Include(x => x.ToUser)
            .Where(x => x.Type == "MainToInvestBonus")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(email))
        {
            mainInvestBonusesQuery = mainInvestBonusesQuery.Where(x =>
                x.FromUser.Email.ToLower().Contains(email) ||
                x.ToUser.Email.ToLower().Contains(email));
        }

        if (fromDate.HasValue)
            mainInvestBonusesQuery = mainInvestBonusesQuery.Where(x => x.CreatedAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            mainInvestBonusesQuery = mainInvestBonusesQuery.Where(x => x.CreatedAt < toDateExclusive.Value);

        if (minAmount.HasValue)
            mainInvestBonusesQuery = mainInvestBonusesQuery.Where(x => x.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            mainInvestBonusesQuery = mainInvestBonusesQuery.Where(x => x.Amount <= maxAmount.Value);

        var mainInvestBonuses = await mainInvestBonusesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminBonusDto
            {
                FromUserEmail = x.FromUser.Email,
                ToUserEmail = x.ToUser.Email,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var response = new AdminDetailReportResponse
        {
            Deposits = deposits,
            Withdrawals = withdrawals,
            Transfers = transfers,
            InvestmentProfits = investmentProfits,
            ReferralProfits = referralProfits,
            MainInvestBonuses = mainInvestBonuses,
            Kycs = kycs
        };

        return ResponseFactory.Success(response);
    }
    public async Task<ApiResponse<object>> GetAdminDetailReportPageAsync(
        AdminReportFilterRequest filter)
    {
        filter ??= new AdminReportFilterRequest();

        var type = filter.Type?.Trim();

        if (string.IsNullOrWhiteSpace(type))
            type = "deposits";

        type = type.Trim();

        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;

        if (pageSize > 100)
            pageSize = 100;

        var email = filter.Email?.Trim().ToLowerInvariant();
        var statusText = filter.Status?.Trim();
        var fromDate = filter.FromDate?.Date;
        var toDateExclusive = filter.ToDate?.Date.AddDays(1);
        var minAmount = filter.MinAmount;
        var maxAmount = filter.MaxAmount;

        var hasStatusFilter =
            !string.IsNullOrWhiteSpace(statusText) &&
            !statusText.Equals("All", StringComparison.OrdinalIgnoreCase);

        DepositStatus parsedStatus = DepositStatus.Pending;
        KycStatus parsedKycStatus = KycStatus.Pending;

        var statusParsed = false;
        var kycStatusParsed = false;

        if (hasStatusFilter)
        {
            statusParsed = Enum.TryParse(statusText, true, out parsedStatus);
            kycStatusParsed = Enum.TryParse(statusText, true, out parsedKycStatus);
        }

        if (type.Equals("deposits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.Deposits
                .AsNoTracking()
                .Include(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.User.Email.ToLower().Contains(email) ||
                    x.User.ReferralCode.ToLower().Contains(email) ||
                    x.User.FirstName.ToLower().Contains(email) ||
                    x.User.LastName.ToLower().Contains(email) ||
                    x.TxId.ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            if (hasStatusFilter && statusParsed)
                query = query.Where(x => x.Status == parsedStatus);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminDepositDto
                {
                    DepositId = x.DepositId,
                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                    Amount = x.Amount,
                    TxId = x.TxId,
                    Status = x.Status.ToString(),
                    AdminNote = x.AdminNote,
                    CreatedAt = x.CreatedAt,
                    ProcessedAt = x.ProcessedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("withdrawals", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.Withdrawals
                .AsNoTracking()
                .Include(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.User.Email.ToLower().Contains(email) ||
                    x.User.ReferralCode.ToLower().Contains(email) ||
                    x.User.FirstName.ToLower().Contains(email) ||
                    x.User.LastName.ToLower().Contains(email) ||
                    x.WalletAddress.ToLower().Contains(email) ||
                    x.Network.ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            if (hasStatusFilter && statusParsed)
                query = query.Where(x => x.Status == parsedStatus);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminWithdrawalDto
                {
                    WithdrawalId = x.WithdrawalId,
                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                    Amount = x.Amount,
                    WalletAddress = x.WalletAddress,
                    Network = x.Network,
                    Status = x.Status.ToString(),
                    AdminNote = x.AdminNote,
                    CreatedAt = x.CreatedAt,
                    ProcessedAt = x.ProcessedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("transfers", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.WalletTransfers
                .AsNoTracking()
                .Include(x => x.FromWallet)
                    .ThenInclude(x => x.User)
                .Include(x => x.ToWallet)
                    .ThenInclude(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.FromWallet.User.Email.ToLower().Contains(email) ||
                    x.ToWallet.User.Email.ToLower().Contains(email) ||
                    x.FromWallet.User.ReferralCode.ToLower().Contains(email) ||
                    x.ToWallet.User.ReferralCode.ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            var totalCount = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.TransferId,
                    x.Amount,
                    x.Description,
                    x.CreatedAt,

                    FromWalletType = x.FromWallet.WalletType,
                    ToWalletType = x.ToWallet.WalletType,

                    FromUserEmail = x.FromWallet.User.Email,
                    FromUserUid = x.FromWallet.User.ReferralCode,

                    ToUserEmail = x.ToWallet.User.Email,
                    ToUserUid = x.ToWallet.User.ReferralCode
                })
                .ToListAsync();

            var items = rows
                .Select(x => new AdminWalletTransactionDto
                {
                    TransferId = x.TransferId,
                    FromUserEmail = x.FromUserEmail,
                    FromUserUid = x.FromUserUid,
                    ToUserEmail = x.ToUserEmail,
                    ToUserUid = x.ToUserUid,
                    FromWalletType = x.FromWalletType.ToString(),
                    ToWalletType = x.ToWalletType.ToString(),
                    Amount = x.Amount,
                    Description = x.Description,
                    CreatedAt = x.CreatedAt
                })
                .ToList();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("investmentProfits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ProfitLedgers
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.Type == ProfitType.Investment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.User.Email.ToLower().Contains(email) ||
                    x.User.ReferralCode.ToLower().Contains(email) ||
                    x.User.FirstName.ToLower().Contains(email) ||
                    x.User.LastName.ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminProfitDto
                {
                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    ProfitType = "Investment Profit",
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("referralProfits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ProfitLedgers
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.SourceUser)
                .Where(x => x.Type == ProfitType.Referral)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.User.Email.ToLower().Contains(email) ||
                    x.User.ReferralCode.ToLower().Contains(email) ||
                    x.User.FirstName.ToLower().Contains(email) ||
                    x.User.LastName.ToLower().Contains(email) ||
                    (x.SourceUser != null && x.SourceUser.Email.ToLower().Contains(email)) ||
                    (x.SourceUser != null && x.SourceUser.ReferralCode.ToLower().Contains(email)) ||
                    (x.SourceUser != null && x.SourceUser.FirstName.ToLower().Contains(email)) ||
                    (x.SourceUser != null && x.SourceUser.LastName.ToLower().Contains(email)));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminProfitDto
                {
                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,

                    SourceUserEmail = x.SourceUser != null
                        ? x.SourceUser.Email
                        : null,

                    SourceUserUid = x.SourceUser != null
                        ? x.SourceUser.ReferralCode
                        : null,

                    SourceUserFullName = x.SourceUser != null
                        ? (x.SourceUser.FirstName + " " + x.SourceUser.LastName).Trim()
                        : null,

                    ProfitType = "Referral Profit",
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("mainInvestBonuses", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ReferralBonusRecords
                .AsNoTracking()
                .Include(x => x.FromUser)
                .Include(x => x.ToUser)
                .Where(x => x.Type == "MainToInvestBonus")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.FromUser.Email.ToLower().Contains(email) ||
                    x.ToUser.Email.ToLower().Contains(email) ||
                    x.FromUser.ReferralCode.ToLower().Contains(email) ||
                    x.ToUser.ReferralCode.ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (minAmount.HasValue)
                query = query.Where(x => x.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(x => x.Amount <= maxAmount.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminBonusDto
                {
                    FromUserEmail = x.FromUser.Email,
                    ToUserEmail = x.ToUser.Email,
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("kycs", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.KycDocuments
                .AsNoTracking()
                .Include(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(x =>
                    x.User.Email.ToLower().Contains(email) ||
                    x.User.ReferralCode.ToLower().Contains(email) ||
                    x.User.FirstName.ToLower().Contains(email) ||
                    x.User.LastName.ToLower().Contains(email) ||
                    x.DocumentType.ToString().ToLower().Contains(email));
            }

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDateExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < toDateExclusive.Value);

            if (hasStatusFilter && kycStatusParsed)
                query = query.Where(x => x.Status == parsedKycStatus);

            var totalCount = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.DocumentId,
                    x.UserId,

                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),

                    DocumentType = x.DocumentType.ToString(),
                    Status = x.Status.ToString(),
                    x.RejectReason,
                    x.CreatedAt,
                    x.ReviewedAt,
                    x.ReviewedByAdminId
                })
                .ToListAsync();

            var reviewerIds = rows
                .Where(x => x.ReviewedByAdminId.HasValue)
                .Select(x => x.ReviewedByAdminId!.Value)
                .Distinct()
                .ToList();

            var reviewers = await _context.Users
                .AsNoTracking()
                .Where(x => reviewerIds.Contains(x.UserId))
                .Select(x => new
                {
                    x.UserId,
                    x.Email,
                    x.ReferralCode
                })
                .ToDictionaryAsync(x => x.UserId);

            var items = rows
                .Select(x =>
                {
                    reviewers.TryGetValue(
                        x.ReviewedByAdminId ?? 0,
                        out var reviewer);

                    return new AdminKycReportDto
                    {
                        DocumentId = x.DocumentId,
                        UserId = x.UserId,
                        UserEmail = x.UserEmail,
                        UserUid = x.UserUid,
                        UserFullName = x.UserFullName,
                        DocumentType = x.DocumentType,
                        Status = x.Status,
                        RejectReason = x.RejectReason,
                        CreatedAt = x.CreatedAt,
                        ReviewedAt = x.ReviewedAt,
                        ReviewedByAdminId = x.ReviewedByAdminId,
                        ReviewedByAdminEmail = reviewer?.Email,
                        ReviewedByAdminUid = reviewer?.ReferralCode
                    };
                })
                .ToList();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        return ResponseFactory.Fail<object>("Invalid report type.");
    }

    public async Task<ApiResponse<object>> GetUserReportSummaryAsync(long userId)
    {
        var totalDeposits = await _context.Deposits
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == DepositStatus.Approved)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalWithdrawals = await _context.Withdrawals
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == DepositStatus.Approved)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalInvestmentProfits = await _context.ProfitLedgers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Type == ProfitType.Investment)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalReferralProfits = await _context.ProfitLedgers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Type == ProfitType.Referral)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var totalBonuses = await _context.ReferralBonusRecords
            .AsNoTracking()
            .Where(x =>
                x.ToUserId == userId &&
                x.Type == "MainToInvestBonus")
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        return ResponseFactory.Success<object>(new
        {
            TotalDeposits = totalDeposits,
            TotalWithdrawals = totalWithdrawals,
            TotalProfits = totalInvestmentProfits + totalReferralProfits,
            TotalBonuses = totalBonuses
        });
    }

    public async Task<ApiResponse<object>> GetUserReportPageAsync(
        long userId,
        string? type,
        int page = 1,
        int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(type))
            type = "deposits";

        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        type = type.Trim();

        if (type.Equals("deposits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.Deposits
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new DepositDto
                {
                    DepositId = x.DepositId,
                    Amount = x.Amount,
                    TxId = x.TxId,
                    Status = x.Status.ToString(),
                    AdminNote = x.AdminNote,
                    CreatedAt = x.CreatedAt,
                    ProcessedAt = x.ProcessedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("withdrawals", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.Withdrawals
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new WithdrawalDto
                {
                    WithdrawalId = x.WithdrawalId,
                    Amount = x.Amount,
                    WalletAddress = x.WalletAddress,
                    Network = x.Network,
                    Status = x.Status.ToString(),
                    AdminNote = x.AdminNote,
                    CreatedAt = x.CreatedAt,
                    ProcessedAt = x.ProcessedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("transfers", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.WalletTransfers
                .AsNoTracking()
                .Include(x => x.FromWallet)
                    .ThenInclude(x => x.User)
                .Include(x => x.ToWallet)
                    .ThenInclude(x => x.User)
                .Where(x =>
                    x.FromWallet.UserId == userId ||
                    x.ToWallet.UserId == userId);

            var totalCount = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.TransferId,
                    x.Amount,
                    x.Description,
                    x.CreatedAt,

                    FromWalletType = x.FromWallet.WalletType,
                    ToWalletType = x.ToWallet.WalletType,

                    FromUserId = x.FromWallet.UserId,
                    FromEmail = x.FromWallet.User.Email,
                    FromUid = x.FromWallet.User.ReferralCode,

                    ToUserId = x.ToWallet.UserId,
                    ToEmail = x.ToWallet.User.Email,
                    ToUid = x.ToWallet.User.ReferralCode
                })
                .ToListAsync();

            var items = rows
                .Select(x => new WalletTransactionDto
                {
                    TransferId = x.TransferId,

                    Direction = x.FromUserId == x.ToUserId
                        ? "Wallet Transfer"
                        : x.FromUserId == userId
                            ? "Sent"
                            : "Received",

                    FromWalletType = x.FromWalletType.ToString(),
                    ToWalletType = x.ToWalletType.ToString(),

                    FromEmail = x.FromEmail,
                    FromUid = x.FromUid,

                    ToEmail = x.ToEmail,
                    ToUid = x.ToUid,

                    Amount = x.Amount,
                    Description = x.Description,
                    CreatedAt = x.CreatedAt
                })
                .ToList();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("investmentProfits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ProfitLedgers
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Type == ProfitType.Investment);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProfitDto
                {
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("referralProfits", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ProfitLedgers
                .AsNoTracking()
                .Include(x => x.SourceUser)
                .Where(x =>
                    x.UserId == userId &&
                    x.Type == ProfitType.Referral);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProfitDto
                {
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt,

                    SourceUserUid = x.SourceUser != null
                        ? x.SourceUser.ReferralCode
                        : null,

                    SourceUserEmail = x.SourceUser != null
                        ? x.SourceUser.Email
                        : null,

                    SourceUserFullName = x.SourceUser != null
                        ? (x.SourceUser.FirstName + " " + x.SourceUser.LastName).Trim()
                        : null
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        if (type.Equals("mainInvestBonuses", StringComparison.OrdinalIgnoreCase))
        {
            var query = _context.ReferralBonusRecords
                .AsNoTracking()
                .Where(x =>
                    x.ToUserId == userId &&
                    x.Type == "MainToInvestBonus");

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new BonusDto
                {
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success<object>(
                CreatePagedResponse(page, pageSize, totalCount, items));
        }

        return ResponseFactory.Fail<object>("Invalid report type.");
    }
    private static PagedResponse<T> CreatePagedResponse<T>(
        int page,
        int pageSize,
        int totalCount,
        List<T> items)
    {
        return new PagedResponse<T>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }
}