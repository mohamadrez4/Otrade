using Hangfire.Dashboard;
using Microsoft.AspNetCore.DataProtection;
using Otrade.Application.Services;
using Otrade.Domain.Enums;

namespace Otrade.web.Security;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IDataProtector _protector;

    public HangfireDashboardAuthorizationFilter(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Otrade.Hangfire.Session");
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (!httpContext.Request.Cookies.TryGetValue("HangfireAuth", out var protectedTicket))
            return false;

        try
        {
            var ticket = _protector.Unprotect(protectedTicket);

            var parts = ticket.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return false;

            if (!long.TryParse(parts[0], out var userId))
                return false;

            if (!long.TryParse(parts[1], out var expiresTicks))
                return false;

            var expiresAt = new DateTimeOffset(expiresTicks, TimeSpan.Zero);

            if (expiresAt <= DateTimeOffset.UtcNow)
                return false;

            var permissionService = httpContext.RequestServices
                .GetRequiredService<AdminPermissionService>();

            var access = permissionService
                .EnsurePermissionAsync(userId, AdminPermission.ManageHangfire)
                .GetAwaiter()
                .GetResult();

            return access.Success;
        }
        catch
        {
            return false;
        }
    }
}