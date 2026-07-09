using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.DTOs.Auth;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Persistence.Context;
using System;
namespace Otrade.web.Controllers;
public class AuthController : Controller
{
    private readonly AuthService _authService;
    private readonly PreRegistrationService _preRegistrationService;
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }
    [HttpGet("/auth/pre-registration-wait")]
    public IActionResult PreRegistrationWait()
    {
        return View();
    }

    [HttpGet("/auth/complete-pre-registration")]
    public IActionResult CompletePreRegistration()
    {
        return View();
    }

    [HttpGet("/auth/recover-pre-registration")]
    public IActionResult RecoverPreRegistration()
    {
        return View();
    }
    public AuthController(
        AuthService authService,
        PreRegistrationService preRegistrationService)
    {
        _authService = authService;
        _preRegistrationService = preRegistrationService;
    }
    [HttpPost]
    [Route("api/auth/pre-register/recover/verify")]
    public async Task<IActionResult> VerifyRecoverPreRegistration(
    [FromBody] VerifyRecoverPreRegistrationRequest request)
    {
        var result = await _preRegistrationService.VerifyRecoveryAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpGet]
    [Route("api/auth/pre-register/deposit-info")]
    public async Task<IActionResult> GetPreRegistrationDepositInfo()
    {
        var result = await _preRegistrationService.GetDepositInfoAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/pre-register/wait-status")]
    public async Task<IActionResult> GetPreRegistrationWaitStatus(
    [FromBody] PreRegistrationWaitStatusRequest request)
    {
        var result = await _preRegistrationService.GetWaitStatusAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost]
    [Route("api/auth/pre-register/recover")]
    public async Task<IActionResult> RecoverPreRegistration(
        [FromBody] RecoverPreRegistrationRequest request)
    {
        var result = await _preRegistrationService.RecoverAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/pre-register/complete")]
    public async Task<IActionResult> CompletePreRegistration(
    [FromBody] CompletePreRegistrationRequest request)
    {
        var result = await _preRegistrationService.CompleteAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/pre-register/deposit")]
    public async Task<IActionResult> SubmitPreRegistrationDeposit(
    [FromBody] SubmitPreRegistrationDepositRequest request)
    {
        var result = await _preRegistrationService.SubmitDepositAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/pre-register/start")]
    public async Task<IActionResult> StartPreRegistration(
    [FromBody] StartPreRegistrationRequest request)
    {
        var result = await _preRegistrationService.StartAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/pre-register/verify-email")]
    public async Task<IActionResult> VerifyPreRegistrationEmail(
    [FromBody] VerifyPreRegistrationEmailRequest request)
    {
        var result = await _preRegistrationService.VerifyEmailAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost]
    [Route("api/auth/verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _authService
            .VerifyEmailAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost]
    [Route("api/auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost]
    [Route("api/auth/forgot-password")]
    public async Task<IActionResult> ForgetPassword([FromBody] ResendVerificationRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpPost]
    [Route("/api/auth/change-password")]
    public async Task<IActionResult> ChangePassowrd([FromBody] ChangePassword request)
    {
        var result = await _authService.ChangePasswordAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}