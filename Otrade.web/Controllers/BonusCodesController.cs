using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers;

public class BonusCodesController : Controller
{
    [HttpGet("/bonus-codes")]
    public IActionResult Index()
    {
        return View();
    }
}