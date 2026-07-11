using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using System.Security.Claims;

namespace Otrade.web.Controllers.Api;

[ApiController]
public class AdminSessionController : ControllerBase
{
    private readonly AdminPermissionService _adminPermissionService;
    private readonly IDataProtector _protector;

    public AdminSessionController(
        AdminPermissionService adminPermissionService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _adminPermissionService = adminPermissionService;
        _protector = dataProtectionProvider.CreateProtector("Otrade.AdminPanel.Session");
    }

    [Authorize]
    [HttpPost("/admin/session")]
    public async Task<IActionResult> CreateSession()
    {
        var userId = GetUserIdFromClaims(User);

        if (userId <= 0)
            return Forbid();

        var access = await _adminPermissionService.GetMyAccessAsync(userId);

        if (!access.Success)
            return Forbid();

        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var ticket = $"{userId}|{expiresAt.UtcTicks}";
        var protectedTicket = _protector.Protect(ticket);

        Response.Cookies.Append("AdminPanelAuth", protectedTicket, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/admin",
            Expires = expiresAt
        });

        return Ok(new
        {
            redirectUrl = "/admin/dashboard"
        });
    }

    private static long GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var value =
            user.FindFirst("userId")?.Value ??
            user.FindFirst("UserId")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            user.FindFirst("nameid")?.Value ??
            user.FindFirst("sub")?.Value;

        return long.TryParse(value, out var userId)
            ? userId
            : 0;
    }
}