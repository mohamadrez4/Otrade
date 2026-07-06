using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Ticket;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class TicketService
{
    private readonly OtradeDbContext _context;
    private readonly SystemSettingService _settingService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IServiceScopeFactory _scopeFactory;
    public TicketService(OtradeDbContext context,
        IServiceScopeFactory scopeFactory,
        SystemSettingService systemSettingService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _settingService = systemSettingService;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
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
        var adminEmail = await _settingService.GetValueAsync("ADMIN_EMAIL");

        var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        var ticketemail = user?.Email ?? userId.ToString();
        var requestsubject = request.Subject;
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var emailTemplateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

                await emailService.SendAsync(
                    adminEmail,
                    "New Ticket Created",
                    emailTemplateService.GetTicketCreatedEmail(
                       ticketemail,
                       requestsubject)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ticket Email Failed: {ex.Message}");
            }
        });
        }
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