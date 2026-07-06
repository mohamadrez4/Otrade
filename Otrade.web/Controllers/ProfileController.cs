using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;
public class ProfileController : Controller
{
    [HttpGet("/profile")]
    public IActionResult Index()
    {
        return View();
    }
}