using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class TicketsController : Controller
    {
        [HttpGet("/tickets")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
