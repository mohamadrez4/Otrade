using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Wallet;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/wallet")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;

    public WalletController(WalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        TransferRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.TransferAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("deposit/request")]
    public async Task<IActionResult> CreateDeposit(
    DepositRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.CreateDepositRequestAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("deposit/info")]
    public async Task<IActionResult> GetDepositInfo()
    {
        var result = await _walletService.GetDepositInfoAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpPost("withdrawal/request")]
    public async Task<IActionResult> CreateWithdrawal(
        WithdrawalRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result =
            await _walletService.CreateWithdrawalRequestAsync(
                request,
                currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("withdrawals/my-pending")]
    public async Task<IActionResult> GetMyPendingWithdrawals(
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetMyPendingWithdrawalsAsync(currentUser.UserId);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("withdrawals/cancel/{id}")]
    public async Task<IActionResult> CancelWithdrawal(
        long id,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.CancelWithdrawalAsync(id, currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpPost("address/save")]
    public async Task<IActionResult> SaveAddress(
        SaveUserWallet request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.SaveWalletAddressAsync(
            request.Address, request.Network, currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("profits")]
    public async Task<IActionResult> GetProfits(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetUserProfitsAsync(currentUser.UserId);
        return Ok(result);
    }
    [HttpGet("referral")]
    public async Task<IActionResult> GetReferral(
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetReferralOverviewAsync(currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}