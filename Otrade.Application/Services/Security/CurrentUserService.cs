using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Otrade.Application.Services.Security;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long UserId =>
        long.Parse(_httpContextAccessor.HttpContext?
            .User.Claims
            .FirstOrDefault(x => x.Type == "userId")?.Value ?? "0");

    public string Email =>
        _httpContextAccessor.HttpContext?
            .User.Claims
            .FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value ?? string.Empty;

    public bool IsAdmin =>
        bool.Parse(_httpContextAccessor.HttpContext?
            .User.Claims
            .FirstOrDefault(x => x.Type == "isAdmin")?.Value ?? "false");

    public bool IsOwner =>
        bool.Parse(_httpContextAccessor.HttpContext?
            .User.Claims
            .FirstOrDefault(x => x.Type == "isOwner")?.Value ?? "false");
}