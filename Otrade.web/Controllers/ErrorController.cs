using Microsoft.AspNetCore.Mvc;
using Otrade.Application.Common;

namespace Otrade.Web.Controllers;

public class ErrorController : Controller
{
    [Route("error/404")]
    public IActionResult NotFoundPage()
    {
        return View("NotFound");
    }

    [Route("error/api-404")]
    public IActionResult ApiNotFound()
    {
        return NotFound(ResponseFactory.Fail<object>("API endpoint not found"));
    }
}