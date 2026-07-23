using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Ticket;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class TicketService
{
    private readonly OtradeDbContext _context;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationQueue _notificationQueue;

    public TicketService(
        OtradeDbContext context,
        IEmailTemplateService emailTemplateService,
        INotificationQueue notificationQueue)
    {
        _context = context;
        _emailTemplateService = emailTemplateService;
        _notificationQueue = notificationQueue;
    }

    public async Task<ApiResponse<TicketResponse>> CreateTicketAsync(
        TicketRequest request,
        long userId)
    {
        if (request == null)
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Invalid request");
        }

        var subject = request.Subject?.Trim() ?? string.Empty;
        var messageText = request.Message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(subject))
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Subject is required");
        }

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Message is required");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory.Fail<TicketResponse>(
                "User not found");
        }

        var now = DateTime.Now;

        var ticket = new Ticket
        {
            UserId = userId,
            Subject = subject,
            Status = "Open",
            CreatedAt = now,
            User = user
        };

        var message = new TicketMessage
        {
            UserId = userId,
            Message = messageText,
            CreatedAt = now,
            Ticket = ticket,
            User = user
        };

        ticket.Messages.Add(message);

        _context.Tickets.Add(ticket);

        await _context.SaveChangesAsync();

        var userFullName = GetUserDisplayName(user);

        var adminEmailBody =
            _emailTemplateService.GetTicketCreatedEmail(
                user.Email,
                subject);

        await _notificationQueue.QueueAdminAsync(
            "New Ticket Created",
            adminEmailBody);

        var userEmailBody =
            _emailTemplateService.GetTicketCreatedEmail(
                user.Email,
                subject);

        await _notificationQueue.QueueEmailAsync(
            user.Email,
            "Ticket Created",
            userEmailBody);

        var response = new TicketResponse
        {
            TicketId = ticket.TicketId,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,

            Messages = new List<TicketMessageDto>
            {
                new()
                {
                    SenderEmail = user.Email,
                    SenderName = userFullName,
                    IsSupport = false,
                    Message = message.Message,
                    CreatedAt = message.CreatedAt
                }
            }
        };

        return ResponseFactory.Success(
            response,
            "Ticket created successfully");
    }

    public async Task<ApiResponse<List<TicketResponse>>>
        GetUserTicketsAsync(long userId)
    {
        var tickets = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Messages)
                .ThenInclude(m => m.User)
            .Where(t =>
                t.UserId == userId)
            .OrderByDescending(t =>
                t.CreatedAt)
            .ToListAsync();

        var response = tickets
            .Select(ticket => new TicketResponse
            {
                TicketId = ticket.TicketId,
                Subject = ticket.Subject,
                Status = ticket.Status,
                CreatedAt = ticket.CreatedAt,

                Messages = ticket.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m =>
                    {
                        var isSupport =
                            m.User.IsAdmin ||
                            m.User.IsOwner;

                        return new TicketMessageDto
                        {
                            SenderEmail =
                                m.User.Email,

                            SenderName =
                                isSupport
                                    ? "Support Team"
                                    : GetUserDisplayName(
                                        m.User),

                            IsSupport =
                                isSupport,

                            Message =
                                m.Message,

                            CreatedAt =
                                m.CreatedAt
                        };
                    })
                    .ToList()
            })
            .ToList();

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<TicketResponse>> ReplyTicketAsync(
        long ticketId,
        string message,
        long userId)
    {
        var messageText =
            message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Message is required");
        }

        /*
         * کنترل امنیتی مهم:
         * کاربر فقط اجازه Reply روی تیکت متعلق به خودش را دارد.
         */
        var ticket = await _context.Tickets
            .Include(t => t.User)
            .Include(t => t.Messages)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t =>
                t.TicketId == ticketId &&
                t.UserId == userId);

        if (ticket == null)
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Ticket not found");
        }

        if (string.Equals(
                ticket.Status,
                "Closed",
                StringComparison.OrdinalIgnoreCase))
        {
            return ResponseFactory.Fail<TicketResponse>(
                "Ticket is closed");
        }

        var now = DateTime.Now;

        var ticketMessage = new TicketMessage
        {
            TicketId = ticket.TicketId,
            UserId = userId,
            Message = messageText,
            CreatedAt = now,
            User = ticket.User
        };

        ticket.Messages.Add(ticketMessage);

        /*
         * وقتی کاربر پاسخ می‌دهد، تیکت دوباره Open می‌شود
         * تا در پنل پشتیبانی قابل پیگیری باشد.
         */
        ticket.Status = "Open";

        await _context.SaveChangesAsync();

        var response = new TicketResponse
        {
            TicketId = ticket.TicketId,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,

            Messages = ticket.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m =>
                {
                    var isSupport =
                        m.User.IsAdmin ||
                        m.User.IsOwner;

                    return new TicketMessageDto
                    {
                        SenderEmail =
                            m.User.Email,

                        SenderName =
                            isSupport
                                ? "Support Team"
                                : GetUserDisplayName(
                                    m.User),

                        IsSupport =
                            isSupport,

                        Message =
                            m.Message,

                        CreatedAt =
                            m.CreatedAt
                    };
                })
                .ToList()
        };

        return ResponseFactory.Success(
            response,
            "Reply sent successfully");
    }

    private static string GetUserDisplayName(User user)
    {
        var fullName =
            $"{user.FirstName} {user.LastName}"
                .Trim();

        return string.IsNullOrWhiteSpace(fullName)
            ? "User"
            : fullName;
    }
}