using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;

public class DepositController : Controller
{
    [HttpGet("/deposit")]
    public IActionResult Index()
    {
        return View();
    }
}