using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class ReportsController : Controller
    {
        [HttpGet("/reports")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
