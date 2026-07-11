using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class AdminAuditService
{
    private readonly OtradeDbContext _context;

    public AdminAuditService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        long actorUserId,
        string action,
        string? entityName = null,
        long? entityId = null,
        long? targetUserId = null,
        string? oldValue = null,
        string? newValue = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var log = new AdminAuditLog
        {
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,

            Action = action,
            EntityName = entityName,
            EntityId = entityId,

            OldValue = oldValue,
            NewValue = newValue,

            IpAddress = ipAddress,
            UserAgent = userAgent,

            CreatedAt = DateTime.UtcNow
        };

        _context.AdminAuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }
}