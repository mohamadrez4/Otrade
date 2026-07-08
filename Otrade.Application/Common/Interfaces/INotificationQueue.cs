using Otrade.Application.Common.Models;

namespace Otrade.Application.Common.Interfaces;

public interface INotificationQueue
{
    ValueTask QueueAdminAsync(string subject, string body);

    ValueTask QueueEmailAsync(string toEmail, string subject, string body);

    IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken cancellationToken);
}