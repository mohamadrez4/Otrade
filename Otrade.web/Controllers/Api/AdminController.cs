using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
using Otrade.Application.DTOs.Ticket;
using Otrade.Application.DTOs.Wallet;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly OtradeDbContext _context;
    private readonly PreRegistrationService _preRegistrationService;
    private readonly InvestmentCapacityService _investmentCapacityService;
    private readonly AdminPermissionService _adminPermissionService;
    private readonly InvestmentWaitListService _investmentWaitListService;
    public AdminController(
    AdminService adminService,
    OtradeDbContext context,
    PreRegistrationService preRegistrationService,
    InvestmentCapacityService investmentCapacityService,
    AdminPermissionService adminPermissionService,
    InvestmentWaitListService investmentWaitListService)
    {
        _adminService = adminService;
        _context = context;
        _preRegistrationService = preRegistrationService;
        _investmentCapacityService = investmentCapacityService;
        _adminPermissionService = adminPermissionService;
        _investmentWaitListService = investmentWaitListService;
    }
    [Authorize]
    [HttpGet("me/access")]
    public async Task<IActionResult> GetMyAdminAccess(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var result = await _adminPermissionService.GetMyAccessAsync(
            currentUser.UserId);

        if (!result.Success)
            return Forbid();

        return Ok(result);
    }
    [Authorize]
    [HttpGet("deposits/pending")]
    public async Task<IActionResult> GetPendingDeposits(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
                currentUser.UserId,
                AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();


        var result = await _adminService.GetPendingDepositsAsync();

        return Ok(result);
    }

    [Authorize]
    [HttpPost("deposits/approve/{id}")]
    public async Task<IActionResult> Approve(
        long id,
        [FromBody] ApproveDepositRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.ApproveDepositAsync(
            id,
            request.ApprovedAmount);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("deposits/reject/{id}")]
    public async Task<IActionResult> Reject(
    long id,
    [FromBody] RejectDepositRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.RejectDepositAsync(
            id,
            request.Reason);

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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageWithdrawals);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageWithdrawals);

        if (!access.Success)
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
        [FromBody] RejectWithdrawalRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageWithdrawals);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.RejectWithdrawalAsync(
            id,
            request.Reason);

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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageKyc);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageKyc);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.ApproveKycDocumentAsync(
                    documentId,
                    currentUser.UserId);

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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageKyc);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.RejectKycDocumentAsync(
            documentId,
            request.Reason,
            currentUser.UserId);

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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageTickets);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageTickets);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageTickets);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageTickets);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageUsers);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.GetUsersAsync(page, pageSize, search);

        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("users/{userId:long}/admin-role")]
    public async Task<IActionResult> UpdateAdminRole(
    long userId,
    [FromBody] UpdateAdminRoleRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageAdminRoles);

        if (!access.Success)
            return Forbid();

        var result = await _adminService.UpdateAdminRoleAsync(
            userId,
            request.AdminRole);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageSettings);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageSettings);

        if (!access.Success)
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
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageKyc);

        if (!access.Success)
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
    [Authorize]
    [HttpGet("pre-registrations/pending")]
    public async Task<IActionResult> GetPendingPreRegistrations(
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _preRegistrationService.GetPendingForAdminAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("pre-registrations/approve/{id}")]
    public async Task<IActionResult> ApprovePreRegistration(
        long id,
        [FromBody] ApprovePreRegistrationRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _preRegistrationService.ApproveAsync(
            id,
            request.ApprovedAmount,
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("pre-registrations/reject/{id}")]
    public async Task<IActionResult> RejectPreRegistration(
        long id,
        [FromBody] RejectPreRegistrationRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _preRegistrationService.RejectAsync(
            id,
            request.Reason);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("investment-capacity")]
    public async Task<IActionResult> GetInvestmentCapacities(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _investmentCapacityService.GetAdminListAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("investment-capacity/save")]
    public async Task<IActionResult> SaveInvestmentCapacity(
        [FromBody] SaveInvestmentCapacityRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();
        var result = await _investmentCapacityService.SaveAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("wallet-summary")]
    public async Task<IActionResult> GetWalletSummary(
    [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();
        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ViewReports);

        if (!access.Success)
            return Forbid();
        var result = await _adminService.GetWalletSummaryAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [Authorize]
    [HttpGet("investment-wait-list")]
    public async Task<IActionResult> GetInvestmentWaitList(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();

        var result = await _investmentWaitListService.GetAdminListAsync(
            page,
            pageSize,
            status,
            search);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("investment-wait-list/{waitListId:long}/status")]
    public async Task<IActionResult> UpdateInvestmentWaitListStatus(
        long waitListId,
        [FromBody] UpdateInvestmentWaitListStatusRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            return Forbid();

        var access = await _adminPermissionService.EnsurePermissionAsync(
            currentUser.UserId,
            AdminPermission.ManageDeposits);

        if (!access.Success)
            return Forbid();

        var result = await _investmentWaitListService.UpdateStatusAsync(
            waitListId,
            request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

}
