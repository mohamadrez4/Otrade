using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;

public class DashboardController : Controller
{
    [HttpGet("/dashboard")]
    public IActionResult Index()
    {
        return View();
    }
}