using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class RankController : Controller
    {
        [HttpGet("/rank")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
