using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Security;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[ApiController]
[Route("api/auth/two-factor/recovery")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public class TwoFactorRecoveryController : ControllerBase
{
    private readonly TwoFactorRecoveryService
        _recoveryService;

    public TwoFactorRecoveryController(
        TwoFactorRecoveryService recoveryService)
    {
        _recoveryService =
            recoveryService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> StartRecovery(
        [FromBody] StartTwoFactorRecoveryRequest request)
    {
        var result =
            await _recoveryService
                .StartRecoveryAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyTwoFactorRecoveryEmailRequest request)
    {
        var result =
            await _recoveryService
                .VerifyRecoveryEmailAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /*
     * POST is intentional:
     * the recovery token will not appear in the URL or server access logs.
     */
    [HttpPost("status")]
    public async Task<IActionResult> GetStatus(
        [FromBody] TwoFactorRecoveryTokenRequest request)
    {
        var result =
            await _recoveryService
                .GetRecoveryStatusAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}