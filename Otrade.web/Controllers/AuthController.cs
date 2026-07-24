using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Auth;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Persistence.Context;
using System;
namespace Otrade.web.Controllers;
public class AuthController : Controller
{
    private const string RefreshTokenCookieName = "__Host-otrade-refresh";
    private readonly AuthService _authService;
    private readonly PreRegistrationService _preRegistrationService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly JwtService _jwtService;
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
        PreRegistrationService preRegistrationService,
        RefreshTokenService refreshTokenService,
        JwtService jwtService)
    {
        _authService = authService;
        _preRegistrationService = preRegistrationService;
        _refreshTokenService = refreshTokenService;
        _jwtService = jwtService;
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
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult>
        CompletePreRegistration(
            [FromBody]
        CompletePreRegistrationRequest request)
    {
        var result =
            await _preRegistrationService
                .CompleteAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        if (
            result.Data == null ||
            string.IsNullOrWhiteSpace(
                result.Data.Token)
        )
        {
            return BadRequest(
                ResponseFactory
                    .Fail<CompletePreRegistrationResponse>(
                        "Registration session could not be created."));
        }

        var refreshCreated =
            await IssueRefreshSessionAsync(
                result.Data.UserId);

        if (!refreshCreated)
        {
            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                ResponseFactory
                    .Fail<CompletePreRegistrationResponse>(
                        "Registration completed but the login session could not be created."));
        }

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
    //[HttpPost]
    //[Route("api/auth/register")]
    //public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    //{
    //    var result = await _authService.RegisterAsync(request);

    //    if (!result.Success)
    //    {
    //        return BadRequest(result);
    //    }

    //    return Ok(result);
    //}

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
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var result =
            await _authService
                .LoginAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        /*
         * در حالت TOTP هنوز Login کامل نشده است.
         * Refresh Token فقط بعد از تأیید 2FA صادر می‌شود.
         */
        if (
            result.Data != null &&
            !result.Data.RequiresTwoFactor &&
            !string.IsNullOrWhiteSpace(
                result.Data.Token)
        )
        {
            var refreshCreated =
                await IssueRefreshSessionAsync(
                    result.Data.UserId);

            if (!refreshCreated)
            {
                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    ResponseFactory
                        .Fail<LoginResponse>(
                            "Login session could not be created."));
            }
        }

        return Ok(result);
    }

    [HttpPost]
    [Route("api/auth/verify-two-factor")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult>
        VerifyTwoFactorLogin(
            [FromBody]
        VerifyLoginTwoFactorRequest request)
    {
        var result =
            await _authService
                .VerifyLoginTwoFactorAsync(
                    request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        if (
            result.Data == null ||
            string.IsNullOrWhiteSpace(
                result.Data.Token)
        )
        {
            return BadRequest(
                ResponseFactory
                    .Fail<LoginResponse>(
                        "Login session could not be created."));
        }

        var refreshCreated =
            await IssueRefreshSessionAsync(
                result.Data.UserId);

        if (!refreshCreated)
        {
            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                ResponseFactory
                    .Fail<LoginResponse>(
                        "Login session could not be created."));
        }

        return Ok(result);
    }
    
    [HttpGet("/auth/two-factor")]
    [ResponseCache(NoStore = true,Location = ResponseCacheLocation.None)]
    public IActionResult TwoFactor()
    {
        return View();
    }
    [HttpGet("/auth/two-factor-recovery")]
    [ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
    public IActionResult TwoFactorRecovery()
    {
        return View();
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
    
    [HttpPost]
    [Route("api/auth/refresh")]
    [EnableRateLimiting("auth-refresh")]
    [ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
    public async Task<IActionResult>
    RefreshAccessToken()
    {
        /*
         * چون این Endpoint با Cookie کار می‌کند،
         * فقط درخواست JavaScript همان سایت را قبول می‌کنیم.
         */
        var requestedWith =
            Request.Headers[
                "X-Requested-With"
            ].ToString();

        if (
            !string.Equals(
                requestedWith,
                "XMLHttpRequest",
                StringComparison.Ordinal)
        )
        {
            return BadRequest(
                ResponseFactory
                    .Fail<RefreshTokenResponse>(
                        "Invalid refresh request."));
        }

        if (
            !Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var rawRefreshToken) ||
            string.IsNullOrWhiteSpace(
                rawRefreshToken)
        )
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(
                ResponseFactory
                    .Fail<RefreshTokenResponse>(
                        "Refresh session not found."));
        }

        var validation =
            await _refreshTokenService
                .ValidateSessionAsync(
                    rawRefreshToken,
                    GetClientIp());

        if (
            !validation.Success ||
            validation.Data?.User == null
        )
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(
                ResponseFactory
                    .Fail<RefreshTokenResponse>(
                        validation.Message));
        }

        var user =
            validation.Data.User;

        var newAccessToken =
            _jwtService.GenerateToken(
                user.UserId,
                user.Email,
                user.IsAdmin,
                user.IsOwner,
                user.AuthTokenVersion,
                user.MustChangePassword);

        var response =
            new RefreshTokenResponse
            {
                Token =
                    newAccessToken,

                UserId =
                    user.UserId,

                IsAdmin =
                    user.IsAdmin,

                IsOwner =
                    user.IsOwner,

                MustChangePassword =
                    user.MustChangePassword
            };

        return Ok(
            ResponseFactory.Success(
                response,
                "Access token refreshed."));
    }
    [HttpPost]
    [Route("api/auth/logout")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LogoutApi()
    {
        if (
            Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var rawRefreshToken) &&
            !string.IsNullOrWhiteSpace(
                rawRefreshToken)
        )
        {
            await _refreshTokenService
                .RevokeSessionAsync(
                    rawRefreshToken);
        }

        DeleteRefreshTokenCookie();

        return Ok(
            ResponseFactory.Success(
                true,
                "Logged out successfully."));
    }
    private async Task<bool>
        IssueRefreshSessionAsync(
            long userId)
    {
        /*
         * اگر این مرورگر قبلاً Session داشته،
         * هنگام Login جدید همان Session را باطل می‌کنیم.
         */
        if (
            Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var previousRefreshToken) &&
            !string.IsNullOrWhiteSpace(
                previousRefreshToken)
        )
        {
            await _refreshTokenService
                .RevokeSessionAsync(
                    previousRefreshToken);
        }

        var session =
            await _refreshTokenService
                .CreateSessionAsync(
                    userId,
                    GetClientIp(),
                    Request.Headers[
                        "User-Agent"
                    ].ToString());

        if (
            !session.Success ||
            session.Data == null
        )
        {
            DeleteRefreshTokenCookie();

            return false;
        }

        WriteRefreshTokenCookie(
            session.Data.RawToken,
            session.Data.ExpiresAt);

        return true;
    }

    private void WriteRefreshTokenCookie(
        string refreshToken,
        DateTime expiresAt)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly =
                    true,

                Secure =
                    true,

                SameSite =
                    SameSiteMode.Strict,

                Path =
                    "/",

                Expires =
                    new DateTimeOffset(
                        DateTime.SpecifyKind(
                            expiresAt,
                            DateTimeKind.Utc)),

                IsEssential =
                    true
            });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly =
                    true,

                Secure =
                    true,

                SameSite =
                    SameSiteMode.Strict,

                Path =
                    "/"
            });
    }

    private string GetClientIp()
    {
        return HttpContext
            .Connection
            .RemoteIpAddress?
            .ToString()
            ?? "unknown";
    }
}