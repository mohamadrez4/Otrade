using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Reports;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Domain.Enums;
namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly AdminPermissionService _adminPermissionService;
    public ReportController(ReportService reportService, AdminPermissionService adminPermissionService)
    {
        _reportService = reportService;
        _adminPermissionService = adminPermissionService;
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
    [HttpGet("user/summary")]
    public async Task<IActionResult> GetUserReportSummary(
       [FromServices] CurrentUserService currentUser)
    {
        var result = await _reportService.GetUserReportSummaryAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("user/page")]
    public async Task<IActionResult> GetUserReportPage(
        [FromQuery] string? type,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _reportService.GetUserReportPageAsync(
            currentUser.UserId,
            type,
            page,
            pageSize);

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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ViewReports);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ViewReports);

        if (!access.Success)
            return Forbid();
        var result = await _reportService.GetAdminDetailReportAsync(filter);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("admin/detail/page")]
    public async Task<IActionResult> GetAdminDetailReportPage(
        [FromQuery] AdminReportFilterRequest filter,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ViewReports);

        if (!access.Success)
            return Forbid();

        var result = await _reportService.GetAdminDetailReportPageAsync(filter);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}