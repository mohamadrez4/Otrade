using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Kyc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly KycService _kycService;

    public KycController(KycService kycService)
    {
        _kycService = kycService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] KycUploadRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _kycService.UploadAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}