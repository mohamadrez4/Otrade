using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.DTOs.Auth;
using Otrade.Application.Services.Security;
using Otrade.Persistence.Context;
using System;
namespace Otrade.web.Controllers;
public class AuthController : Controller
{
    private readonly AuthService _authService;
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
    public AuthController(AuthService authService)
    {
        _authService = authService;
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