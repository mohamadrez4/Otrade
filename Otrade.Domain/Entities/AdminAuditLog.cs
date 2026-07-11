namespace Otrade.Domain.Entities;

public class AdminAuditLog
{
    public long AdminAuditLogId { get; set; }

    public long ActorUserId { get; set; }

    public long? TargetUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public long? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? ActorUser { get; set; }

    public User? TargetUser { get; set; }
}