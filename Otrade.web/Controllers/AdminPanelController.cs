using Microsoft.AspNetCore.Mvc;

namespace Otrade.Web.Controllers;

public class AdminPanelController : Controller
{
    [HttpGet("/admin/dashboard")]
    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet("/admin/deposits")]
    public IActionResult Deposits()
    {
        return View();
    }

    [HttpGet("/admin/withdrawals")]
    public IActionResult Withdrawals()
    {
        return View();
    }

    [HttpGet("/admin/kyc")]
    public IActionResult Kyc()
    {
        return View();
    }

    [HttpGet("/admin/tickets")]
    public IActionResult Tickets()
    {
        return View();
    }

    [HttpGet("/admin/users")]
    public IActionResult Users()
    {
        return View();
    }

    [HttpGet("/admin/reports")]
    public IActionResult Reports()
    {
        return View();
    }

    [HttpGet("/admin/settings")]
    public IActionResult Settings()
    {
        return View();
    }
}