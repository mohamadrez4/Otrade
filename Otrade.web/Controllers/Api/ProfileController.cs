using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Profile;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfileController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _profileService.GetProfileAsync(currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _profileService.ChangePasswordAsync(
            currentUser.UserId,
            request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}