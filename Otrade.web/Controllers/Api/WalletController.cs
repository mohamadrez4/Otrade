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
    private readonly InvestmentCapacityService _investmentCapacityService;
    private readonly InvestmentWaitListService _investmentWaitListService;
    public WalletController(
        WalletService walletService,
        InvestmentCapacityService investmentCapacityService,
        InvestmentWaitListService investmentWaitListService)
    {
        _walletService = walletService;
        _investmentCapacityService = investmentCapacityService;
        _investmentWaitListService = investmentWaitListService;
    }

    [Authorize]
    [HttpGet("internal-transfer/recipient")]
    public async Task<IActionResult> FindInternalTransferRecipient(
    [FromQuery] string query,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.FindInternalTransferRecipientAsync(
            query,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpPost("internal-transfer/send-code")]
    public async Task<IActionResult> CreateInternalTransferVerification(
    [FromBody] CreateInternalTransferVerificationRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.CreateInternalTransferVerificationAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpPost("internal-transfer/confirm")]
    public async Task<IActionResult> ConfirmInternalTransfer(
    [FromBody] ConfirmInternalTransferRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.ConfirmInternalTransferAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
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
    [HttpGet("deposits/my-history")]
    public async Task<IActionResult> GetMyDeposits(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetMyDepositsAsync(
            currentUser.UserId,
            page,
            pageSize);

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
    [HttpGet("withdrawals/my-history")]
    public async Task<IActionResult> GetMyWithdrawals(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetMyWithdrawalsAsync(
            currentUser.UserId,
            page,
            pageSize);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpPost("withdrawal/confirm")]
    public async Task<IActionResult> ConfirmWithdrawal(
    ConfirmWithdrawalRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.ConfirmWithdrawalRequestAsync(
            request,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

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
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? type,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetUserProfitsAsync(
            currentUser.UserId,
            page,
            pageSize,
            type);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("referral")]
    public async Task<IActionResult> GetReferral(
    [FromQuery] int referralsPage,
    [FromQuery] int referralsPageSize,
    [FromQuery] int bonusesPage,
    [FromQuery] int bonusesPageSize,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _walletService.GetReferralOverviewAsync(
            currentUser.UserId,
            referralsPage,
            referralsPageSize,
            bonusesPage,
            bonusesPageSize);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("investment-capacity/current")]
    public async Task<IActionResult> GetCurrentInvestmentCapacity()
    {
        var result = await _investmentCapacityService.GetCurrentMonthCapacityAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpPost("investment-wait-list/join")]
    public async Task<IActionResult> JoinInvestmentWaitList(
    [FromBody] JoinInvestmentWaitListRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _investmentWaitListService.JoinAsync(
            currentUser.UserId,
            request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("investment-wait-list/my-status")]
    public async Task<IActionResult> GetMyInvestmentWaitListStatus(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _investmentWaitListService.GetMyStatusAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("investment-wait-list/cancel")]
    public async Task<IActionResult> CancelMyInvestmentWaitList(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _investmentWaitListService.CancelMyRequestAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}