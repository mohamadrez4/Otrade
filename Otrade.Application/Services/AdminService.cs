using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.DTOs.Common;
using Otrade.Application.DTOs.Ticket;
using Otrade.Application.DTOs.Wallet;
using Otrade.Application.Services.Security;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System.Data;
using System.Security.Cryptography.X509Certificates;
namespace Otrade.Application.Services
{
    public class AdminService
    {
        private readonly OtradeDbContext _context;
        private readonly SystemSettingService _settingService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly EncryptionService _encryptionService;
        private readonly INotificationQueue _notificationQueue;
        public AdminService(OtradeDbContext context,
        IServiceScopeFactory scopeFactory,
        SystemSettingService systemSettingService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
         EncryptionService encryptionService,
         INotificationQueue notificationQueue
        )
        {
            _context = context;

            _settingService = systemSettingService;
            _emailTemplateService = emailTemplateService;
            _encryptionService = encryptionService;
            _notificationQueue= notificationQueue;
        }

        public async Task<ApiResponse<List<DepositsPending>>> GetPendingDepositsAsync()
        {
            var deposits = await _context.Deposits
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.Status == DepositStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DepositsPending
                {
                    DepositId = x.DepositId,
                    Email = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                    TxId = x.TxId,
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success(deposits);
        }
        public async Task<ApiResponse<bool>> ApproveDepositAsync(
            long depositId,
            decimal approvedAmount)
        {
            if (approvedAmount <= 0)
                return ResponseFactory.Fail<bool>("Approved amount must be greater than zero");

            var deposit = await _context.Deposits
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.DepositId == depositId);

            if (deposit == null)
                return ResponseFactory.Fail<bool>("Deposit not found");

            if (deposit.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Already processed");

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(x =>
                    x.UserId == deposit.UserId &&
                    x.WalletType == WalletType.Main);

            if (wallet == null)
                return ResponseFactory.Fail<bool>("Main wallet not found");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var now = DateTime.Now;
            var requestedAmount = deposit.Amount;

            var before = wallet.Balance;
            var after = before + approvedAmount;

            wallet.Balance = after;

            deposit.Amount = approvedAmount;
            deposit.Status = DepositStatus.Approved;
            deposit.ProcessedAt = now;
            deposit.AdminNote = $"Requested: {requestedAmount}, Approved: {approvedAmount}";

            _context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = deposit.UserId,
                WalletId = wallet.WalletId,
                Amount = approvedAmount,
                BalanceBefore = before,
                BalanceAfter = after,
                Type = TransactionType.Deposit,
                Description = $"Deposit approved. Requested: {requestedAmount}, Approved: {approvedAmount}, TxId: {deposit.TxId}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var emailBody = _emailTemplateService.GetDepositApprovedEmail(
                requestedAmount,
                approvedAmount);

            await _notificationQueue.QueueEmailAsync(
                deposit.User.Email,
                "Deposit Approved",
                emailBody);

            return ResponseFactory.Success(true, "Deposit approved and credited to Main Wallet");
        }
        public async Task<ApiResponse<bool>> RejectDepositAsync(
            long depositId,
            string reason)
        {
            var rejectReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(rejectReason))
                return ResponseFactory.Fail<bool>("Reject reason is required");

            if (rejectReason.Length > 1000)
                return ResponseFactory.Fail<bool>("Reject reason cannot be more than 1000 characters");

            var deposit = await _context.Deposits
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.DepositId == depositId);

            if (deposit == null)
                return ResponseFactory.Fail<bool>("Deposit not found");

            if (deposit.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Already processed");

            deposit.Status = DepositStatus.Rejected;
            deposit.AdminNote = rejectReason;
            deposit.ProcessedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            var emailBody = _emailTemplateService.GetDepositRejectedEmail(deposit.Amount, rejectReason);

            await _notificationQueue.QueueEmailAsync(
                deposit.User.Email,
                "Deposit Status",
                emailBody);
            return ResponseFactory.Success(true, "Deposit rejected");
        }
        public async Task<ApiResponse<List<WithdrawalPending>>> GetPendingWithdrawalsAsync()
        {
            var withdrawals = await _context.Withdrawals
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.Status == DepositStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WithdrawalPending
                {
                    WithdrawalId = x.WithdrawalId,

                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),

                    Amount = x.Amount,
                    WalletAddress = x.WalletAddress,
                    Network = x.Network,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success(withdrawals);
        }
        public async Task<ApiResponse<bool>> ApproveWithdrawalAsync(long withdrawalId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            var withdrawal = await _context.Withdrawals
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.WithdrawalId == withdrawalId);

            if (withdrawal == null)
                return ResponseFactory.Fail<bool>("Withdrawal not found");

            if (withdrawal.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Withdrawal is not pending");

            var now = DateTime.Now;

            withdrawal.Status = DepositStatus.Approved;
            withdrawal.ProcessedAt = now;
            withdrawal.AdminNote = "Approved by admin";

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var emailBody = _emailTemplateService.GetWithdrawalApprovedEmail(
                withdrawal.Amount);

            await _notificationQueue.QueueEmailAsync(
                withdrawal.User.Email,
                "Withdrawal Status",
                emailBody);

            return ResponseFactory.Success(true, "Withdrawal approved");
        }
        public async Task<ApiResponse<bool>> RejectWithdrawalAsync(
           long withdrawalId,
           string reason)
        {
            var rejectReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(rejectReason))
                return ResponseFactory.Fail<bool>("Reject reason is required");

            if (rejectReason.Length > 1000)
                return ResponseFactory.Fail<bool>("Reject reason cannot be more than 1000 characters");

            var withdrawal = await _context.Withdrawals
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.WithdrawalId == withdrawalId);

            if (withdrawal == null)
                return ResponseFactory.Fail<bool>("Withdrawal not found");

            if (withdrawal.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Withdrawal is not pending");

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(x =>
                    x.UserId == withdrawal.UserId &&
                    x.WalletType == WalletType.Main);

            if (wallet == null)
                return ResponseFactory.Fail<bool>("Main wallet not found");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var now = DateTime.Now;

            var before = wallet.Balance;
            var after = before + withdrawal.Amount;

            wallet.Balance = after;

            withdrawal.Status = DepositStatus.Rejected;
            withdrawal.AdminNote = rejectReason;
            withdrawal.ProcessedAt = now;

            _context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = withdrawal.UserId,
                WalletId = wallet.WalletId,
                Amount = withdrawal.Amount,
                BalanceBefore = before,
                BalanceAfter = after,
                Type = TransactionType.Adjustment,
                Description = $"Withdrawal rejected and refunded. Reason: {rejectReason}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var emailBody = _emailTemplateService.GetWithdrawalRejectedEmail(
                withdrawal.Amount,
                rejectReason);

            await _notificationQueue.QueueEmailAsync(
                withdrawal.User.Email,
                "Withdrawal Rejected",
                emailBody);

            return ResponseFactory.Success(true, "Withdrawal rejected");
        }
        public async Task<ApiResponse<List<PendingKycDto>>>GetPendingKycsAsync()
        {
            var kycs = await _context.KycDocuments
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.Status == KycStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new PendingKycDto
                {
                    DocumentId = x.DocumentId,
                    UserId = x.UserId,
                    UserEmail = x.User.Email,
                    UserUid = x.User.ReferralCode,
                    UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                    DocumentType = x.DocumentType.ToString(),
                    Status = x.Status.ToString(),
                    FilePath = x.FilePath,  
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success(kycs);
        }
        public async Task<ApiResponse<bool>> ApproveKycDocumentAsync(
            long documentId,
            long adminId)
        {
            var doc = await _context.KycDocuments
                .FirstOrDefaultAsync(x => x.DocumentId == documentId);

            if (doc == null)
                return ResponseFactory.Fail<bool>("Document not found");

            if (doc.Status != KycStatus.Pending)
                return ResponseFactory.Fail<bool>("Only pending documents can be approved");

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == doc.UserId);

            if (user == null)
                return ResponseFactory.Fail<bool>("User not found");

            var now = DateTime.Now;

            doc.Status = KycStatus.Approved;
            doc.RejectReason = null;
            doc.ReviewedAt = now;
            doc.ReviewedByAdminId = adminId;

            var userDocs = await _context.KycDocuments
                .Where(x => x.UserId == doc.UserId)
                .ToListAsync();

            var hasApprovedNationalId = userDocs.Any(x =>
                x.DocumentType == KycDocumentType.NationalId &&
                x.Status == KycStatus.Approved);

            var hasApprovedSelfie = userDocs.Any(x =>
                x.DocumentType == KycDocumentType.Selfie &&
                x.Status == KycStatus.Approved);

            var isFullyApproved = hasApprovedNationalId && hasApprovedSelfie;

            user.KycStatus = isFullyApproved
                ? KycStatus.Approved
                : KycStatus.Pending;

            user.UpdatedAt = now;

            await _context.SaveChangesAsync();

            if (isFullyApproved)
            {
                var emailBody = _emailTemplateService.GetKycApprovedEmail();

                await _notificationQueue.QueueEmailAsync(
                    user.Email,
                    "KYC Approved",
                    emailBody);
            }

            return ResponseFactory.Success(true, "KYC document approved");
        }
        public async Task<ApiResponse<bool>> RejectKycDocumentAsync(
            long documentId,
            string reason,
            long adminId)
        {
            var rejectReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(rejectReason))
                return ResponseFactory.Fail<bool>("Reject reason is required");

            if (rejectReason.Length > 1000)
                return ResponseFactory.Fail<bool>("Reject reason cannot be more than 1000 characters");

            var doc = await _context.KycDocuments
                .FirstOrDefaultAsync(x => x.DocumentId == documentId);

            if (doc == null)
                return ResponseFactory.Fail<bool>("Document not found");

            if (doc.Status != KycStatus.Pending)
                return ResponseFactory.Fail<bool>("Only pending documents can be rejected");

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == doc.UserId);

            if (user == null)
                return ResponseFactory.Fail<bool>("User not found");

            var now = DateTime.Now;

            doc.Status = KycStatus.Rejected;
            doc.RejectReason = rejectReason;
            doc.ReviewedAt = now;
            doc.ReviewedByAdminId = adminId;

            user.KycStatus = KycStatus.Rejected;
            user.UpdatedAt = now;

            await _context.SaveChangesAsync();

            var emailBody = _emailTemplateService.GetKycRejectedEmail(rejectReason);

            await _notificationQueue.QueueEmailAsync(
                user.Email,
                "KYC Document Status Update",
                emailBody);

            return ResponseFactory.Success(true, "KYC document rejected");
        }
        public async Task<ApiResponse<List<AdminTicketDto>>> GetOpenTicketsAsync()
        {
            var tickets = await _context.Tickets
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Messages)
                    .ThenInclude(x => x.User)
                .Where(x => x.Status == "Open")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AdminTicketDto
                {
                    TicketId = x.TicketId,
                    UserEmail = x.User.Email,
                    Subject = x.Subject,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    ClosedAt = x.ClosedAt,
                    Messages = x.Messages
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new AdminTicketMessageDto
                        {
                            SenderEmail = m.User.Email,
                            Message = m.Message,
                            CreatedAt = m.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            return ResponseFactory.Success(tickets);
        }
        public async Task<ApiResponse<bool>> ReplyTicketAsync(
            long ticketId,
            string message,
            long adminUserId)
        {
            if (string.IsNullOrWhiteSpace(message))
                return ResponseFactory.Fail<bool>("Message is required");

            var ticket = await _context.Tickets
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.TicketId == ticketId);

            if (ticket == null)
                return ResponseFactory.Fail<bool>("Ticket not found");

            if (ticket.Status == "Closed")
                return ResponseFactory.Fail<bool>("Ticket is closed");

            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                UserId = adminUserId,
                Message = message.Trim(),
                CreatedAt =  DateTime.Now
            };

            ticket.Status = "Answered";

            _context.TicketMessages.Add(ticketMessage);

            await _context.SaveChangesAsync();

 
            var ticketUserEmail = ticket.User.Email;
            var ticketSubject = ticket.Subject;
            var replyMessage = message.Trim();

            var emailBody = _emailTemplateService.GetTicketReplyEmail(
                ticketSubject,
                replyMessage);

            await _notificationQueue.QueueEmailAsync(
                ticketUserEmail,
                "Ticket Reply",
                emailBody);

            return ResponseFactory.Success(true, "Reply sent");
        }
        public async Task<ApiResponse<bool>> CloseTicketAsync(long ticketId)
        {
            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(x => x.TicketId == ticketId);

            if (ticket == null)
                return ResponseFactory.Fail<bool>("Ticket not found");

            if (ticket.Status == "Closed")
                return ResponseFactory.Fail<bool>("Ticket already closed");

            ticket.Status = "Closed";
            ticket.ClosedAt =  DateTime.Now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Success(true, "Ticket closed");
        }
        public async Task<ApiResponse<PagedResponse<AdminTicketDto>>>GetTicketsAsync(TicketQueryRequest request)
        {
            if (request.Page <= 0)
                request.Page = 1;

            if (request.PageSize <= 0)
                request.PageSize = 20;

            if (request.PageSize > 100)
                request.PageSize = 100;

            var query = _context.Tickets
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Messages)
                    .ThenInclude(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.Status == request.Status);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                query = query.Where(x =>
                    x.Subject.Contains(request.Search) ||
                    x.User.Email.Contains(request.Search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new AdminTicketDto
                {
                    TicketId = x.TicketId,
                    UserEmail = x.User.Email,
                    Subject = x.Subject,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    ClosedAt = x.ClosedAt,
                    Messages = x.Messages
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new AdminTicketMessageDto
                        {
                            SenderEmail = m.User.Email,
                            Message = m.Message,
                            CreatedAt = m.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            var result = new PagedResponse<AdminTicketDto>
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize),
                Items = items
            };

            return ResponseFactory.Success(result);
        }
        public async Task<ApiResponse<PagedResponse<AdminUserDto>>> GetUsersAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null)
        {
            if (page <= 0)
                page = 1;

            if (pageSize <= 0)
                pageSize = 20;

            if (pageSize > 100)
                pageSize = 100;

            var query = _context.Users
                .AsNoTracking()
                .Include(x => x.Sponsor)
                .Include(x => x.Wallets)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();

                query = query.Where(x =>
                    x.Email.Contains(searchText) ||
                    x.FirstName.Contains(searchText) ||
                    x.LastName.Contains(searchText) ||
                    x.ReferralCode.Contains(searchText) ||
                    (x.Sponsor != null && x.Sponsor.Email.Contains(searchText)) ||
                    (x.Sponsor != null && x.Sponsor.ReferralCode.Contains(searchText)) ||
                    (x.Sponsor != null && x.Sponsor.FirstName.Contains(searchText)) ||
                    (x.Sponsor != null && x.Sponsor.LastName.Contains(searchText)));
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminUserDto
                {
                    UserId = x.UserId,
                    Email = x.Email,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    ReferralCode = x.ReferralCode,
                    KycStatus = x.KycStatus.ToString(),

                    SponsorEmail = x.Sponsor != null
                        ? x.Sponsor.Email
                        : null,

                    SponsorFullName = x.Sponsor != null
                        ? (x.Sponsor.FirstName + " " + x.Sponsor.LastName).Trim()
                        : null,

                    SponsorReferralCode = x.Sponsor != null
                        ? x.Sponsor.ReferralCode
                        : null,

                    CurrentRank = _context.Ranks
                        .Where(r => r.RankId == x.CurrentRankId)
                        .Select(r => new RankDto
                        {
                            RankId = r.RankId,
                            Name = r.Name
                        })
                        .FirstOrDefault(),

                    Wallets = new UserWalletsDto
                    {
                        Main = x.Wallets
                            .Where(w => w.WalletType == WalletType.Main)
                            .Select(w => (decimal?)w.Balance)
                            .FirstOrDefault() ?? 0,

                        Invest = x.Wallets
                            .Where(w => w.WalletType == WalletType.Invest)
                            .Select(w => (decimal?)w.Balance)
                            .FirstOrDefault() ?? 0,

                        Profit = x.Wallets
                            .Where(w => w.WalletType == WalletType.Profit)
                            .Select(w => (decimal?)w.Balance)
                            .FirstOrDefault() ?? 0,

                        Referral = x.Wallets
                            .Where(w => w.WalletType == WalletType.Referral)
                            .Select(w => (decimal?)w.Balance)
                            .FirstOrDefault() ?? 0
                    }
                })
                .ToListAsync();

            var result = new PagedResponse<AdminUserDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = users
            };

            return ResponseFactory.Success(result);
        }
        public async Task<ApiResponse<List<SystemSettingDto>>> GetSettingsAsync()
        {
            var settings = await _context.SystemSettings
                .OrderBy(x => x.Key)
                .Select(x => new SystemSettingDto
                {
                    Key = x.Key,
                    Value = x.Value
                })
                .ToListAsync();

            return ResponseFactory.Success(settings);
        }

        public async Task<ApiResponse<bool>> SaveSettingsAsync(SaveSystemSettingsRequest request)
        {
            if (request.Settings == null || !request.Settings.Any())
                return ResponseFactory.Fail<bool>("No settings provided");

            foreach (var item in request.Settings)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                    continue;

                var key = item.Key.Trim();
                var value = item.Value?.Trim() ?? "";

                var isSensitiveSetting =
                    key == "Password" ||
                    key == "TelegramBotToken";

                if (isSensitiveSetting &&
                    !string.IsNullOrWhiteSpace(value) &&
                    !value.StartsWith("ENC:", StringComparison.OrdinalIgnoreCase))
                {
                    value = _encryptionService.Encrypt(value);
                }
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(x => x.Key == key);

                if (setting == null)
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        Key = key,
                        Value = value
                    });
                }
                else
                {
                    if (isSensitiveSetting && string.IsNullOrWhiteSpace(item.Value))
                        continue;

                    setting.Value = value;

                }
            }
            await _context.SaveChangesAsync();

            _settingService.ClearCache();


            return ResponseFactory.Success(true, "Settings saved successfully");
        }
        public async Task<ApiResponse<AdminWalletSummaryResponse>> GetWalletSummaryAsync()
        {
            var groupedWallets = await _context.Wallets
                .AsNoTracking()
                .GroupBy(x => x.WalletType)
                .Select(x => new
                {
                    WalletType = x.Key,
                    Total = x.Sum(w => w.Balance),
                    Count = x.Count()
                })
                .ToListAsync();

            decimal GetTotal(WalletType walletType)
            {
                return groupedWallets
                    .FirstOrDefault(x => x.WalletType == walletType)
                    ?.Total ?? 0;
            }

            var usersWithBalance = await _context.Wallets
                .AsNoTracking()
                .Where(x => x.Balance > 0)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();

            var totalWallets = await _context.Wallets
                .AsNoTracking()
                .CountAsync();

            var totalMain = GetTotal(WalletType.Main);
            var totalInvest = GetTotal(WalletType.Invest);
            var totalProfit = GetTotal(WalletType.Profit);
            var totalReferral = GetTotal(WalletType.Referral);

            var response = new AdminWalletSummaryResponse
            {
                TotalMainWallet = totalMain,
                TotalInvestWallet = totalInvest,
                TotalProfitWallet = totalProfit,
                TotalReferralWallet = totalReferral,
                TotalAssets = totalMain + totalInvest + totalProfit + totalReferral,
                TotalWallets = totalWallets,
                UsersWithBalance = usersWithBalance
            };

            return ResponseFactory.Success(response);
        }
    }
}