using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Otrade.Application.Services
{
    public class AdminService
    {
        private readonly OtradeDbContext _context;
        private readonly SystemSettingService _settingService;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EncryptionService _encryptionService;
        public AdminService(OtradeDbContext context,
        IServiceScopeFactory scopeFactory,
        SystemSettingService systemSettingService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
         EncryptionService encryptionService
        )
        {
            _context = context;

            _scopeFactory = scopeFactory;
            _settingService = systemSettingService;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _encryptionService = encryptionService;
        }

        public async Task<ApiResponse<List<DepositsPending>>>GetPendingDepositsAsync()
        {
            var deposits = await _context.Deposits
                .Include(x => x.User)
                .Where(x => x.Status == DepositStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DepositsPending
                {
                    depositId = x.DepositId,
                    Email = x.User.Email,
                    TxId=x.TxId,
                    Amount = x.Amount,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success(deposits);
        }
        public async Task<ApiResponse<bool>> ApproveDepositAsync(long depositId)
        {
            var deposit = await _context.Deposits
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
                return ResponseFactory.Fail<bool>("Wallet not found");

            var before = wallet.Balance;
            var after = before + deposit.Amount;

            wallet.Balance = after;

            deposit.Status = DepositStatus.Approved;

            var tx = new WalletTransaction
            {
                UserId = deposit.UserId,
                WalletId = wallet.WalletId,
                Amount = deposit.Amount,
                BalanceBefore = before,
                BalanceAfter = after,
                Type = TransactionType.Deposit,
                Description = $"Deposit approved (TxId: {deposit.TxId})",
                CreatedAt =  DateTime.Now
            };

            _context.WalletTransactions.Add(tx);
            await _context.SaveChangesAsync();

            return ResponseFactory.Success(true, "Deposit approved");
        }
        public async Task<ApiResponse<bool>> RejectDepositAsync(long depositId)
        {
            var deposit = await _context.Deposits
                .FirstOrDefaultAsync(x => x.DepositId == depositId);

            if (deposit == null)
                return ResponseFactory.Fail<bool>("Deposit not found");

            if (deposit.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Already processed");

            deposit.Status = DepositStatus.Rejected;

            await _context.SaveChangesAsync();

            return ResponseFactory.Success(true, "Deposit rejected");
        }
        public async Task<ApiResponse<List<WithdrawalPending>>>GetPendingWithdrawalsAsync()
        {
            var withdrawals = await _context.Withdrawals
                .Include(x => x.User)
                .Where(x => x.Status == DepositStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WithdrawalPending
                {
                    WithdrawalId = x.WithdrawalId,
                    UserEmail = x.User.Email,
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
            var withdrawal = await _context.Withdrawals
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.WithdrawalId == withdrawalId);

            if (withdrawal == null)
                return ResponseFactory.Fail<bool>("Withdrawal not found");

            if (withdrawal.Status != DepositStatus.Pending)
                return ResponseFactory.Fail<bool>("Withdrawal is not pending");

            withdrawal.Status = DepositStatus.Approved;
            withdrawal.ProcessedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            var amount = withdrawal.Amount;
            var useremail = withdrawal.User.Email;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                        useremail,
                        "Withdrawal Status",
                           emailTemplateService.GetWithdrawalApprovedEmail(amount)
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
                }
            });
            return ResponseFactory.Success(true, "Withdrawal approved");
        }
        //public async Task<ApiResponse<bool>>ApproveWithdrawalAsync(long withdrawalId)
        //{
        //    var withdrawal = await _context.Withdrawals
        //        .Include(x=> x.User)
        //        .FirstOrDefaultAsync(x =>
        //            x.WithdrawalId == withdrawalId);

        //    if (withdrawal == null)
        //        return ResponseFactory.Fail<bool>(
        //            "Withdrawal not found");

        //    if (withdrawal.Status != DepositStatus.Pending)
        //        return ResponseFactory.Fail<bool>(
        //            "Already processed");

        //    var wallet = await _context.Wallets

        //        .FirstOrDefaultAsync(x =>
        //            x.UserId == withdrawal.UserId &&
        //            x.WalletType == WalletType.Main);

        //    if (wallet == null)
        //        return ResponseFactory.Fail<bool>(
        //            "Main wallet not found");

        //    if (wallet.Balance < withdrawal.Amount)
        //        return ResponseFactory.Fail<bool>(
        //            "Insufficient balance");

        //    var before = wallet.Balance;

        //    wallet.Balance -= withdrawal.Amount;

        //    withdrawal.Status = DepositStatus.Approved;
        //    withdrawal.ProcessedAt =  DateTime.Now;

        //    _context.WalletTransactions.Add(
        //        new WalletTransaction
        //        {
        //            UserId = wallet.UserId,
        //            WalletId = wallet.WalletId,
        //            Amount = -withdrawal.Amount,
        //            BalanceBefore = before,
        //            BalanceAfter = wallet.Balance,
        //            Type = TransactionType.Withdrawal,
        //            Description = "Withdrawal approved",
        //            CreatedAt =  DateTime.Now
        //        });

        //    await _context.SaveChangesAsync();
        //    var amount = withdrawal.Amount;
        //    var useremail = withdrawal.User.Email;
        //    _ = Task.Run(async () =>
        //      {
        //          try
        //          {
        //              using var scope = _scopeFactory.CreateScope();

        //              var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        //              var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

        //              await emailService.SendAsync(
        //                  useremail,
        //                  "Withdrawal Status",
        //                     emailTemplateService.GetWithdrawalApprovedEmail(amount)
        //              );
        //          }
        //          catch (Exception ex)
        //          {
        //              Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
        //          }
        //        });
        // return ResponseFactory.Success(
        //        true,
        //        "Withdrawal approved");
        //}

        public async Task<ApiResponse<bool>> RejectWithdrawalAsync(long withdrawalId)
        {
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

            var before = wallet.Balance;
            var after = before + withdrawal.Amount;

            wallet.Balance = after;

            withdrawal.Status = DepositStatus.Rejected;
            withdrawal.ProcessedAt = DateTime.Now;

            _context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = withdrawal.UserId,
                WalletId = wallet.WalletId,
                Amount = withdrawal.Amount,
                BalanceBefore = before,
                BalanceAfter = after,
                Type = TransactionType.Adjustment,
                Description = "Withdrawal rejected and refunded",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            var amount = withdrawal.Amount;

            var useremail = withdrawal.User.Email;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                       useremail,
                        "Withdrawal Status",
                            emailTemplateService.GetWithdrawalRejectedEmail(amount, "Withdrawal rejected")
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
                }
            });
            return ResponseFactory.Success(true, "Withdrawal rejected");
        }
        //public async Task<ApiResponse<bool>>RejectWithdrawalAsync(long withdrawalId)
        //{
        //    var withdrawal = await _context.Withdrawals
        //        .Include(x => x.User)
        //        .FirstOrDefaultAsync(x =>
        //            x.WithdrawalId == withdrawalId);

        //    if (withdrawal == null)
        //        return ResponseFactory.Fail<bool>(
        //            "Withdrawal not found");

        //    if (withdrawal.Status != DepositStatus.Pending)
        //        return ResponseFactory.Fail<bool>(
        //            "Already processed");

        //    withdrawal.Status = DepositStatus.Rejected;
        //    withdrawal.ProcessedAt =  DateTime.Now;
        //    await _context.SaveChangesAsync();
        //    var amount = withdrawal.Amount;

        //    var useremail = withdrawal.User.Email;
        //    _ = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            using var scope = _scopeFactory.CreateScope();

        //            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        //            var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

        //            await emailService.SendAsync(
        //               useremail,
        //                "Withdrawal Status",
        //                    emailTemplateService.GetWithdrawalRejectedEmail(amount, "Withdrawal rejected")
        //            );
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Withdrawal Email Failed: {ex.Message}");
        //        }
        //    });
        //    return ResponseFactory.Success(
        //        true,
        //        "Withdrawal rejected");
        //}
        public async Task<ApiResponse<List<PendingKycDto>>>GetPendingKycsAsync()
        {
            var kycs = await _context.KycDocuments
                .Include(x => x.User)
                .Where(x => x.Status == KycStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new PendingKycDto
                {
                    DocumentId = x.DocumentId,
                    UserId = x.UserId,
                    UserEmail = x.User.Email,
                    DocumentType = x.DocumentType.ToString(),
                    Status = x.Status.ToString(),
                    FilePath = x.FilePath,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return ResponseFactory.Success(kycs);
        }
        public async Task<ApiResponse<bool>> ApproveKycDocumentAsync(long documentId)
        {
            var doc = await _context.KycDocuments
                .FirstOrDefaultAsync(x => x.DocumentId == documentId);

            if (doc == null)
                return ResponseFactory.Fail<bool>("Document not found");

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == doc.UserId);

            if (user == null)
                return ResponseFactory.Fail<bool>("User not found");

            doc.Status = KycStatus.Approved;

            var userDocs = await _context.KycDocuments
                .Where(x => x.UserId == doc.UserId)
                .ToListAsync();

            var hasApprovedNationalId = userDocs.Any(x =>
                x.DocumentType == KycDocumentType.NationalId &&
                x.Status == KycStatus.Approved);

            var hasApprovedSelfie = userDocs.Any(x =>
                x.DocumentType == KycDocumentType.Selfie &&
                x.Status == KycStatus.Approved);

            user.KycStatus = hasApprovedNationalId && hasApprovedSelfie
                ? KycStatus.Approved
                : KycStatus.Pending;

            user.UpdatedAt =  DateTime.Now;

            await _context.SaveChangesAsync();

            return ResponseFactory.Success(true, "KYC document approved");
        }
        public async Task<ApiResponse<bool>> RejectKycDocumentAsync(long documentId, string reason)
        {
            var doc = await _context.KycDocuments
                .FirstOrDefaultAsync(x => x.DocumentId == documentId);

            if (doc == null)
                return ResponseFactory.Fail<bool>("Document not found");

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == doc.UserId);

            if (user == null)
                return ResponseFactory.Fail<bool>("User not found");

            // به جای حذف رکورد، فقط وضعیت را رد کن
            doc.Status = KycStatus.Rejected;
            doc.CreatedAt =  DateTime.Now; // می‌توان تاریخ آپدیت را هم بروزرسانی کرد

            user.KycStatus = KycStatus.Pending; // وضعیت کلی KYC هنوز Pending است
            user.UpdatedAt =  DateTime.Now;

            await _context.SaveChangesAsync();

            // ارسال ایمیل به کاربر
            var useremail = user.Email;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                        useremail,
                        "KYC Document Status Update",
                        emailTemplateService.GetKycRejectedEmail(reason)
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"KYC Email Failed: {ex.Message}");
                }
            });

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

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                    await emailService.SendAsync(
                        ticketUserEmail,
                        "Ticket Reply",
                        emailTemplateService.GetTicketReplyEmail(ticketSubject, replyMessage)
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ticket Reply Email Failed: {ex.Message}");
                }
            });

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
            int page=1,
            int pageSize=20,
            string? search=null)
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
                query = query.Where(x =>
                    x.Email.Contains(search) ||
                    x.FirstName.Contains(search) ||
                    x.LastName.Contains(search));
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
                    KycStatus = x.KycStatus.ToString(),
                    SponsorEmail = x.Sponsor != null ? x.Sponsor.Email : null,

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

                if (key == "Password" && !string.IsNullOrWhiteSpace(value))
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
                    if (key == "Password" && string.IsNullOrWhiteSpace(item.Value))
                        continue;

                    setting.Value = value;
                }
            }
            await _context.SaveChangesAsync();

            return ResponseFactory.Success(true, "Settings saved successfully");
        }
    }
}