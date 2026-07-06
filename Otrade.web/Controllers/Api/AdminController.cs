using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.DTOs.Ticket;
using Otrade.Application.DTOs.Wallet;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Persistence.Context;
namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly OtradeDbContext _context;

    public AdminController(
        AdminService adminService,
        OtradeDbContext context)
    {
        _adminService = adminService;
        _context = context;
    }

    [Authorize]
    [HttpGet("deposits/pending")]
    public async Task<IActionResult> GetPendingDeposits(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetPendingDepositsAsync();

        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("deposits/approve/{id}")]
    public async Task<IActionResult> Approve(long id,[FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.ApproveDepositAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("deposits/reject/{id}")]
    public async Task<IActionResult> Reject(
        long id,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.RejectDepositAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("withdrawals/pending")]
    public async Task<IActionResult> GetPendingWithdrawals(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetPendingWithdrawalsAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("withdrawals/approve/{id}")]
    public async Task<IActionResult> ApproveWithdrawal(
        long id,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.ApproveWithdrawalAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("withdrawals/reject/{id}")]
    public async Task<IActionResult> RejectWithdrawal(
        long id,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.RejectWithdrawalAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("kyc/pending")]
    public async Task<IActionResult> GetPendingKycs(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetPendingKycsAsync();

        return Ok(result);
    }

    [HttpPost("kyc/document/{documentId}/approve")]
    public async Task<IActionResult> ApproveKycDocument(long documentId,
         [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var result = await _adminService.ApproveKycDocumentAsync(documentId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("kyc/document/{documentId}/reject")]
    public async Task<IActionResult> RejectKycDocument(
        long documentId,
        [FromBody] RejectKycRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var result = await _adminService.RejectKycDocumentAsync(
            documentId,
            request.Reason);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }


    [Authorize]
    [HttpGet("tickets/open")]
    public async Task<IActionResult> GetOpenTickets(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetOpenTicketsAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("tickets/reply/{ticketId}")]
    public async Task<IActionResult> ReplyTicket(
        long ticketId,
        AdminTicketReplyRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(ResponseFactory.Fail<bool>("Message is required"));

        var result = await _adminService.ReplyTicketAsync(
            ticketId,
            request.Message,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("tickets/close/{ticketId}")]
    public async Task<IActionResult> CloseTicket(
        long ticketId,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.CloseTicketAsync(ticketId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(
    [FromQuery] TicketQueryRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetTicketsAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page,
        [FromQuery] int pageSize ,
        [FromQuery] string? search ,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin) return Forbid();

        var result = await _adminService.GetUsersAsync(page, pageSize, search);

        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.GetSettingsAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("settings/save")]
    public async Task<IActionResult> SaveSettings(
        [FromBody] SaveSystemSettingsRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var result = await _adminService.SaveSettingsAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("kyc/document/{documentId}/file")]
    public async Task<IActionResult> GetKycDocumentFile(
    long documentId,
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var doc = await _context.KycDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == documentId);

        if (doc == null)
            return NotFound();

        var fullPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "App_Data",
            doc.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var extension = Path.GetExtension(fullPath).ToLower();

        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        return PhysicalFile(fullPath, contentType);
    }
}
