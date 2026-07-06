using Microsoft.AspNetCore.Mvc;

namespace Otrade.web.Controllers
{
    public class ReferralController : Controller
    {
        [HttpGet("/referral")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
