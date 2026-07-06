using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;

[Authorize]
[ApiController]
[Route("api/contract")]
public class ContractController : ControllerBase
{ 
    private readonly ContractService _service;
    private readonly CurrentUserService _current;


    public ContractController(ContractService service, CurrentUserService current)
    {
        _service = service;
        _current = current;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create()
    {
        var result = await _service.CreateContractAsync(_current.UserId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        var result = await _service.GetCurrentContractAsync(_current.UserId);
        return Ok(result);
    }
}