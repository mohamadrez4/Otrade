using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _dashboardService.GetDashboardAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}