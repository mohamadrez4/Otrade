using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;
public class ContractsController : Controller
{
    [HttpGet("/contracts")]
    public IActionResult Index()
    {
        return View();
    }
}