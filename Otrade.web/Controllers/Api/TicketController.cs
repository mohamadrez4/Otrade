using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.DTOs.Ticket;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

namespace Otrade.web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/tickets")]
public class TicketController : ControllerBase
{
    private readonly TicketService _ticketService;

    public TicketController(TicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(
    [FromBody] TicketRequest request,
    [FromServices] CurrentUserService currentUser)
    {
        var result = await _ticketService.CreateTicketAsync(
            request, currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("reply/{ticketId}")]
    public async Task<IActionResult> Reply(
        long ticketId,
        [FromBody] TicketReplyRequest request,
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _ticketService.ReplyTicketAsync(
            ticketId, request.Message, currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    [HttpGet("my-tickets")]
    public async Task<IActionResult> GetUserTickets(
        [FromServices] CurrentUserService currentUser)
    {
        var result = await _ticketService.GetUserTicketsAsync(
            currentUser.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }


}