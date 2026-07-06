using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class ProfitsController : Controller
    {
        [HttpGet("/profits")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
