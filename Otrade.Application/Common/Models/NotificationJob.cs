namespace Otrade.Application.Common.Models;

public enum NotificationJobType
{
    Admin,
    Email
}

public class NotificationJob
{
    public NotificationJobType Type { get; set; }

    public string? ToEmail { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}