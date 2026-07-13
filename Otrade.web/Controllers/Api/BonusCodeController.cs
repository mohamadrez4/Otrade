using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Bonus;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/bonus-codes")]
public class BonusCodeController : ControllerBase
{
    private readonly BonusCodeService _bonusCodeService;

    public BonusCodeController(BonusCodeService bonusCodeService)
    {
        _bonusCodeService = bonusCodeService;
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyBonusCodeRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _bonusCodeService.ApplyAsync(
            currentUser.UserId,
            request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("my-usages")]
    public async Task<IActionResult> GetMyUsages(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _bonusCodeService.GetMyUsagesAsync(
            currentUser.UserId,
            page,
            pageSize);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}