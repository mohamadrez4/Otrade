using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Web.Services;

namespace Otrade.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/rank")]
public class RankController : ControllerBase
{
    private readonly RankService _rankService;
    private readonly CurrentUserService _currentUser;

    public RankController(
        RankService rankService,
        CurrentUserService currentUser)
    {
        _rankService = rankService;
        _currentUser = currentUser;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var result = await _rankService.GetUserRankOverviewAsync(_currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}