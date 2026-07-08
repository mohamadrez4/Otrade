using Otrade.Application.Common.Interfaces;
using Otrade.Application.Common.Models;
using System.Threading.Channels;

namespace Otrade.Application.Services;

public class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationJob> _queue;

    public NotificationQueue()
    {
        _queue = Channel.CreateUnbounded<NotificationJob>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }

    public ValueTask QueueAdminAsync(string subject, string body)
    {
        return _queue.Writer.WriteAsync(new NotificationJob
        {
            Type = NotificationJobType.Admin,
            Subject = subject,
            Body = body
        });
    }

    public ValueTask QueueEmailAsync(string toEmail, string subject, string body)
    {
        return _queue.Writer.WriteAsync(new NotificationJob
        {
            Type = NotificationJobType.Email,
            ToEmail = toEmail,
            Subject = subject,
            Body = body
        });
    }

    public IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}