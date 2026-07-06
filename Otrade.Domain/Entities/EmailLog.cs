namespace Otrade.Domain.Entities;

public class EmailLog
{
    public long Id { get; set; }

    public string ToEmail { get; set; }
    public string Subject { get; set; }

    public string Body { get; set; }

    public string Status { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }
}