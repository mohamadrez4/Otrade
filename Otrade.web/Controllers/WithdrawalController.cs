using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class WithdrawalController : Controller
    {
        [HttpGet("/withdrawal")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
