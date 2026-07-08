using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.Common.Models;

namespace Otrade.web.BackgroundServices;

public class NotificationBackgroundWorker : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(
        INotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var notificationService =
                    scope.ServiceProvider.GetRequiredService<INotificationService>();

                switch (job.Type)
                {
                    case NotificationJobType.Admin:
                        await notificationService.NotifyAdminAsync(
                            job.Subject,
                            job.Body);
                        break;

                    case NotificationJobType.Email:
                        if (!string.IsNullOrWhiteSpace(job.ToEmail))
                        {
                            await notificationService.SendEmailAsync(
                                job.ToEmail,
                                job.Subject,
                                job.Body);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification background job failed.");
            }
        }
    }
}