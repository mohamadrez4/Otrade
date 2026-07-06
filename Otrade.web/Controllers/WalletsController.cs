using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;

public class WalletsController : Controller
{
    [HttpGet("/wallets")]
    public IActionResult Index()
    {
        return View();
    }
}