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
        TicketRequest request, long userId)
    {
        var ticket = new Ticket
        {
            UserId = userId,
            Subject = request.Subject,
            Status = "Open",
            CreatedAt =  DateTime.Now
        };

        var message = new TicketMessage
        {
            UserId = userId,
            Message = request.Message,
            CreatedAt =  DateTime.Now,
            Ticket = ticket
        };

        ticket.Messages.Add(message);

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();


        var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        var ticketEmail = user?.Email ?? userId.ToString();
        var adminEmailBody = _emailTemplateService.GetTicketCreatedEmail(
                    ticketEmail,
                    request.Subject);

        await _notificationQueue.QueueAdminAsync(
            "New Ticket Created",
            adminEmailBody);

        var userEmailBody = _emailTemplateService.GetTicketCreatedEmail(
            ticketEmail,
            request.Subject);

        await _notificationQueue.QueueEmailAsync(
            ticketEmail,
            "Ticket Created",
            userEmailBody);
        var response = new TicketResponse
        {
            TicketId = ticket.TicketId,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,
            Messages = ticket.Messages.Select(m => new TicketMessageDto
            {
                SenderEmail = m.User.Email,
                Message = m.Message,
                CreatedAt = m.CreatedAt
            }).ToList()
        };

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<List<TicketResponse>>> GetUserTicketsAsync(long userId)
    {
        var tickets = await _context.Tickets
            .Include(t => t.Messages)
            .ThenInclude(m => m.User)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var response = tickets.Select(ticket => new TicketResponse
        {
            TicketId = ticket.TicketId,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,
            Messages = ticket.Messages.Select(m => new TicketMessageDto
            {
                SenderEmail = m.User.Email,
                Message = m.Message,
                CreatedAt = m.CreatedAt
            }).ToList()
        }).ToList();

        return ResponseFactory.Success(response);
    }

    public async Task<ApiResponse<TicketResponse>> ReplyTicketAsync(
        long ticketId, string message, long userId)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Messages)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
            return ResponseFactory.Fail<TicketResponse>("Ticket not found");
        if (ticket.Status== "Closed")
            return ResponseFactory.Fail<TicketResponse>("Ticket is closed");

        var msg = new TicketMessage
        {
            TicketId = ticket.TicketId,
            UserId = userId,
            Message = message,
            CreatedAt =  DateTime.Now
        };

        ticket.Messages.Add(msg);

        await _context.SaveChangesAsync();

        var response = new TicketResponse
        {
            TicketId = ticket.TicketId,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,
            Messages = ticket.Messages.Select(m => new TicketMessageDto
            {
                SenderEmail = m.User.Email,
                Message = m.Message,
                CreatedAt = m.CreatedAt
            }).ToList()
        };

        return ResponseFactory.Success(response);
    }
}