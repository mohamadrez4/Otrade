using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Security;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Domain.Enums;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/admin/two-factor-recovery")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public class AdminTwoFactorRecoveryController : ControllerBase
{
    private readonly TwoFactorRecoveryService
        _recoveryService;

    private readonly AdminPermissionService
        _adminPermissionService;

    public AdminTwoFactorRecoveryController(
        TwoFactorRecoveryService recoveryService,
        AdminPermissionService adminPermissionService)
    {
        _recoveryService =
            recoveryService;

        _adminPermissionService =
            adminPermissionService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin &&
            !currentUser.IsOwner)
        {
            return Forbid();
        }

        var access =
            await _adminPermissionService
                .EnsurePermissionAsync(
                    currentUser.UserId,
                    AdminPermission.ManageKyc);

        if (!access.Success)
        {
            return Forbid();
        }

        var result =
            await _recoveryService
                .GetPendingAdminReviewsAsync();

        return Ok(result);
    }

    [HttpPost("{requestId:long}/approve")]
    public async Task<IActionResult> Approve(
        long requestId,
        [FromBody] ReviewTwoFactorRecoveryRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin &&
            !currentUser.IsOwner)
        {
            return Forbid();
        }

        var access =
            await _adminPermissionService
                .EnsurePermissionAsync(
                    currentUser.UserId,
                    AdminPermission.ManageKyc);

        if (!access.Success)
        {
            return Forbid();
        }

        var result =
            await _recoveryService
                .ApproveRecoveryAsync(
                    requestId,
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{requestId:long}/reject")]
    public async Task<IActionResult> Reject(
        long requestId,
        [FromBody] ReviewTwoFactorRecoveryRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin &&
            !currentUser.IsOwner)
        {
            return Forbid();
        }

        var access =
            await _adminPermissionService
                .EnsurePermissionAsync(
                    currentUser.UserId,
                    AdminPermission.ManageKyc);

        if (!access.Success)
        {
            return Forbid();
        }

        var result =
            await _recoveryService
                .RejectRecoveryAsync(
                    requestId,
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}