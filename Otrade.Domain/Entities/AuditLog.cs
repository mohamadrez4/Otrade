namespace Otrade.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string Action { get; set; }

    public string Details { get; set; }

    public DateTime CreatedAt { get; set; }
}