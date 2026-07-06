namespace Otrade.Domain.Entities;

public class Ticket
{
    public long TicketId { get; set; }

    public long UserId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Status { get; set; } = "Open"; // Open, Closed

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public User User { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

public class TicketMessage
{
    public long MessageId { get; set; }

    public long TicketId { get; set; }

    public long UserId { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Ticket Ticket { get; set; }

    public User User { get; set; }
}