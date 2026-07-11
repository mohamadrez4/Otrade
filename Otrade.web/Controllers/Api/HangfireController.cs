using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Domain.Enums;

[ApiController]
public class HangfireAccessController : ControllerBase
{
    private readonly AdminPermissionService _adminPermissionService;
    private readonly IDataProtector _protector;

    public HangfireAccessController(
        AdminPermissionService adminPermissionService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _adminPermissionService = adminPermissionService;
        _protector = dataProtectionProvider.CreateProtector("Otrade.Hangfire.Session");
    }

    [Authorize]
    [HttpPost("/admin/hangfire/session")]
    public async Task<IActionResult> CreateSession(
        [FromServices] CurrentUserService currentUser)
    {
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageHangfire);

        if (!access.Success)
            return Forbid();

        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);

        var ticket = $"{currentUser.UserId}|{expiresAt.UtcTicks}";
        var protectedTicket = _protector.Protect(ticket);

        Response.Cookies.Append("HangfireAuth", protectedTicket, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/hangfire",
            Expires = expiresAt
        });

        return Ok(new
        {
            redirectUrl = "/hangfire"
        });
    }
}