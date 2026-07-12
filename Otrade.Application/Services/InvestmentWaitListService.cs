using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.DTOs.Common;
using Otrade.Application.DTOs.Wallet;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class InvestmentWaitListService
{
    private readonly OtradeDbContext _context;
    private readonly INotificationQueue _notificationQueue;
    private readonly IEmailTemplateService _emailTemplateService;

    public InvestmentWaitListService(
        OtradeDbContext context,
        INotificationQueue notificationQueue,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _notificationQueue = notificationQueue;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<ApiResponse<MyInvestmentWaitListDto>> JoinAsync(
        long userId,
        JoinInvestmentWaitListRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<MyInvestmentWaitListDto>("Invalid request");

        if (request.RequestedAmount <= 0)
            return ResponseFactory.Fail<MyInvestmentWaitListDto>("Requested amount must be greater than zero");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<MyInvestmentWaitListDto>("User not found");

        var activeEntry = await _context.InvestmentWaitListEntries
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                (
                    x.Status == InvestmentWaitListStatus.Pending ||
                    x.Status == InvestmentWaitListStatus.Notified ||
                    x.Status == InvestmentWaitListStatus.CapacityAvailable
                ));

        var now = DateTime.Now;

        if (activeEntry != null)
        {
            activeEntry.RequestedAmount = request.RequestedAmount;
            activeEntry.Status = InvestmentWaitListStatus.Pending;
            activeEntry.NotifiedAt = null;
            activeEntry.CompletedAt = null;
            activeEntry.AdminNote = null;

            await _context.SaveChangesAsync();

            return ResponseFactory.Success(
                MapMyDto(activeEntry),
                "Your wait list request was updated");
        }

        var entry = new InvestmentWaitListEntry
        {
            UserId = userId,
            RequestedAmount = request.RequestedAmount,
            Status = InvestmentWaitListStatus.Pending,
            CreatedAt = now
        };

        _context.InvestmentWaitListEntries.Add(entry);

        await _context.SaveChangesAsync();

        var body = _emailTemplateService.GetInvestmentWaitListJoinedEmail(
            request.RequestedAmount);

        await _notificationQueue.QueueEmailAsync(
            user.Email,
            "Investment Wait List Request",
            body);

        return ResponseFactory.Success(
            MapMyDto(entry),
            "You have been added to the investment wait list");
    }

    public async Task<ApiResponse<MyInvestmentWaitListDto?>> GetMyStatusAsync(long userId)
    {
        var entry = await _context.InvestmentWaitListEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (entry == null)
            return ResponseFactory.Success<MyInvestmentWaitListDto?>(null);

        return ResponseFactory.Success<MyInvestmentWaitListDto?>(
            MapMyDto(entry));
    }

    public async Task<ApiResponse<bool>> CancelMyRequestAsync(long userId)
    {
        var entry = await _context.InvestmentWaitListEntries
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                (
                    x.Status == InvestmentWaitListStatus.Pending ||
                    x.Status == InvestmentWaitListStatus.Notified ||
                    x.Status == InvestmentWaitListStatus.CapacityAvailable
                ));

        if (entry == null)
            return ResponseFactory.Fail<bool>("Active wait list request not found");

        entry.Status = InvestmentWaitListStatus.Cancelled;
        entry.CompletedAt = DateTime.Now;
        entry.AdminNote = "Cancelled by user";

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "Wait list request cancelled");
    }

    public async Task<ApiResponse<PagedResponse<AdminInvestmentWaitListDto>>> GetAdminListAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? search = null)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.InvestmentWaitListEntries
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<InvestmentWaitListStatus>(
                    status,
                    true,
                    out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.User.Email.ToLower().Contains(text) ||
                x.User.ReferralCode.ToLower().Contains(text) ||
                x.User.FirstName.ToLower().Contains(text) ||
                x.User.LastName.ToLower().Contains(text));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminInvestmentWaitListDto
            {
                WaitListId = x.WaitListId,
                UserId = x.UserId,
                UserEmail = x.User!.Email,
                UserUid = x.User.ReferralCode,
                UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                RequestedAmount = x.RequestedAmount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt,
                NotifiedAt = x.NotifiedAt,
                CompletedAt = x.CompletedAt,
                AdminNote = x.AdminNote
            })
            .ToListAsync();

        var response = new PagedResponse<AdminInvestmentWaitListDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(
        long waitListId,
        UpdateInvestmentWaitListStatusRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<bool>("Invalid request");

        if (!Enum.TryParse<InvestmentWaitListStatus>(
                request.Status,
                true,
                out var status))
        {
            return ResponseFactory.Fail<bool>("Invalid wait list status");
        }

        var entry = await _context.InvestmentWaitListEntries
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.WaitListId == waitListId);

        if (entry == null)
            return ResponseFactory.Fail<bool>("Wait list entry not found");
        if (entry.Status == InvestmentWaitListStatus.Cancelled)
            return ResponseFactory.Fail<bool>("Cancelled wait list request cannot be changed");

        if (entry.Status == InvestmentWaitListStatus.Completed)
            return ResponseFactory.Fail<bool>("Completed wait list request cannot be changed");
        entry.Status = status;
        entry.AdminNote = request.AdminNote?.Trim();

        if (status == InvestmentWaitListStatus.Notified)
        {
            entry.NotifiedAt = DateTime.Now;
        }

        if (status == InvestmentWaitListStatus.CapacityAvailable)
        {
            entry.NotifiedAt = DateTime.Now;
        }

        if (status is InvestmentWaitListStatus.Cancelled or InvestmentWaitListStatus.Completed)
        {
            entry.CompletedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        if (entry.User != null && status == InvestmentWaitListStatus.Notified)
        {
            var body = _emailTemplateService.GetInvestmentWaitListNotifiedEmail(
                entry.RequestedAmount);

            await _notificationQueue.QueueEmailAsync(
                entry.User.Email,
                "Investment Wait List Update",
                body);
        }

        if (entry.User != null && status == InvestmentWaitListStatus.CapacityAvailable)
        {
            var body = _emailTemplateService.GetInvestmentCapacityAvailableEmail(
                entry.RequestedAmount);

            await _notificationQueue.QueueEmailAsync(
                entry.User.Email,
                "Investment Capacity Available",
                body);
        }

        return ResponseFactory.Success(true, "Wait list status updated");
    }

    public async Task NotifyPendingUsersIfCapacityAvailableAsync(decimal availableCapacity)
    {
        if (availableCapacity <= 0)
            return;

        var entries = await _context.InvestmentWaitListEntries
            .Include(x => x.User)
            .Where(x =>
                x.Status == InvestmentWaitListStatus.Pending &&
                x.RequestedAmount <= availableCapacity)
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync();

        if (!entries.Any())
            return;

        var now = DateTime.Now;

        foreach (var entry in entries)
        {
            if (entry.User == null)
                continue;

            entry.Status = InvestmentWaitListStatus.CapacityAvailable;
            entry.NotifiedAt = now;

            var body = _emailTemplateService.GetInvestmentCapacityAvailableEmail(
                entry.RequestedAmount);

            await _notificationQueue.QueueEmailAsync(
                entry.User.Email,
                "Investment Capacity Available",
                body);
        }

        await _context.SaveChangesAsync();
    }

    private static MyInvestmentWaitListDto MapMyDto(InvestmentWaitListEntry entry)
    {
        return new MyInvestmentWaitListDto
        {
            WaitListId = entry.WaitListId,
            RequestedAmount = entry.RequestedAmount,
            Status = entry.Status.ToString(),
            CreatedAt = entry.CreatedAt,
            NotifiedAt = entry.NotifiedAt,
            CompletedAt = entry.CompletedAt,
            AdminNote = entry.AdminNote
        };
    }
}