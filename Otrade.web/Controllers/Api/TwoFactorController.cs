using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Security;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/security/two-factor")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public class TwoFactorController : ControllerBase
{
    private readonly TwoFactorAuthenticationService
        _twoFactorService;

    private readonly TwoFactorRecoveryService
        _twoFactorRecoveryService;

    public TwoFactorController(
        TwoFactorAuthenticationService twoFactorService,
        TwoFactorRecoveryService twoFactorRecoveryService)
    {
        _twoFactorService =
            twoFactorService;

        _twoFactorRecoveryService =
            twoFactorRecoveryService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorService
                .GetStatusAsync(
                    currentUser.UserId);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("setup")]
    public async Task<IActionResult> CreateSetup(
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorService
                .CreateSetupAsync(
                    currentUser.UserId);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(
        [FromBody] VerifyTwoFactorSetupRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorService
                .EnableAsync(
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(
        [FromBody] DisableTwoFactorRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorService
                .DisableAsync(
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("recovery-codes/regenerate")]
    public async Task<IActionResult> RegenerateRecoveryCodes(
        [FromBody] RegenerateRecoveryCodesRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorService
                .RegenerateRecoveryCodesAsync(
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("replace/start")]
    public async Task<IActionResult> StartReplacement(
        [FromBody] StartTwoFactorReplacementRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorRecoveryService
                .StartReplacementAsync(
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("replace/confirm")]
    public async Task<IActionResult> ConfirmReplacement(
        [FromBody] ConfirmTwoFactorReplacementRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _twoFactorRecoveryService
                .ConfirmReplacementAsync(
                    currentUser.UserId,
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}