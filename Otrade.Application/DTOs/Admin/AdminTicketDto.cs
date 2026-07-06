namespace Otrade.Application.DTOs.Admin;

public class AdminTicketDto
{
    public long TicketId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public List<AdminTicketMessageDto> Messages { get; set; } = new();
}

public class AdminTicketMessageDto
{
    public string SenderEmail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}