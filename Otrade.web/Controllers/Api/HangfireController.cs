using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class HangfireAccessController : ControllerBase
{
    [Authorize]
    [HttpPost("/admin/hangfire/session")]
    public IActionResult CreateSession()
    {
        var isAdmin = User.Claims.Any(c =>
            c.Type.Equals("isAdmin", StringComparison.OrdinalIgnoreCase) &&
            c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

        var isOwner = User.Claims.Any(c =>
            c.Type.Equals("isOwner", StringComparison.OrdinalIgnoreCase) &&
            c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

        if (!isAdmin && !isOwner)
            return Forbid();

        Response.Cookies.Append("HangfireAuth", "true", new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.Now.AddHours(2)
        });

        return Ok();
    }
}