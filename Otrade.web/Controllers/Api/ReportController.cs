using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Application.DTOs.Reports;
namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserReport(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _reportService.GetUserReportAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    
    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminReport(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _reportService.GetAdminReportAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("admin/detail")]
    public async Task<IActionResult> GetAdminDetailReport(
        [FromQuery] AdminReportFilterRequest filter,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _reportService.GetAdminDetailReportAsync(filter);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}