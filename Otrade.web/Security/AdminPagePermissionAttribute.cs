using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Otrade.Application.Services;
using Otrade.Domain.Enums;

namespace Otrade.web.Security;

public class AdminPagePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly AdminPermission _permission;

    public AdminPagePermissionAttribute(AdminPermission permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Cookies.TryGetValue("AdminPanelAuth", out var protectedTicket))
        {
            context.Result = new RedirectResult("/admin/forbidden");
            return;
        }

        long userId;

        try
        {
            var dataProtectionProvider = httpContext.RequestServices
                .GetRequiredService<IDataProtectionProvider>();

            var protector = dataProtectionProvider
                .CreateProtector("Otrade.AdminPanel.Session");

            var ticket = protector.Unprotect(protectedTicket);

            var parts = ticket.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                context.Result = new RedirectResult("/admin/forbidden");
                return;
            }

            if (!long.TryParse(parts[0], out userId))
            {
                context.Result = new RedirectResult("/admin/forbidden");
                return;
            }

            if (!long.TryParse(parts[1], out var expiresTicks))
            {
                context.Result = new RedirectResult("/admin/forbidden");
                return;
            }

            var expiresAt = new DateTimeOffset(expiresTicks, TimeSpan.Zero);

            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                context.Result = new RedirectResult("/admin/forbidden");
                return;
            }
        }
        catch
        {
            context.Result = new RedirectResult("/admin/forbidden");
            return;
        }

        var permissionService = httpContext.RequestServices
            .GetRequiredService<AdminPermissionService>();

        var access = await permissionService.EnsurePermissionAsync(
            userId,
            _permission);

        if (!access.Success)
        {
            context.Result = new RedirectResult("/admin/forbidden");
        }
    }
}