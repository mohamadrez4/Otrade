using Microsoft.AspNetCore.Mvc;
using Otrade.Domain.Enums;
using Otrade.web.Security;

namespace Otrade.Web.Controllers;

public class AdminPanelController : Controller
{
    [HttpGet("/admin/dashboard")]
    public IActionResult Dashboard()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageDeposits)]
    [HttpGet("/admin/deposits")]
    public IActionResult Deposits()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageWithdrawals)]
    [HttpGet("/admin/withdrawals")]
    public IActionResult Withdrawals()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageKyc)]
    [HttpGet("/admin/kyc")]
    public IActionResult Kyc()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageTickets)]
    [HttpGet("/admin/tickets")]
    public IActionResult Tickets()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageUsers)]
    [HttpGet("/admin/users")]
    public IActionResult Users()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ViewReports)]
    [HttpGet("/admin/reports")]
    public IActionResult Reports()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ViewReports)]
    [HttpGet("/admin/wallet-overview")]
    public IActionResult WalletOverview()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageSettings)]
    [HttpGet("/admin/settings")]
    public IActionResult Settings()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageDeposits)]
    [HttpGet("/admin/pre-registrations")]
    public IActionResult PreRegistrations()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageDeposits)]
    [HttpGet("/admin/investment-capacity")]
    public IActionResult InvestmentCapacity()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageDeposits)]
    [HttpGet("/admin/investment-wait-list")]
    public IActionResult InvestmentWaitList()
    {
        return View();
    }
    [HttpGet("/admin/forbidden")]
    public IActionResult Forbidden()
    {
        return View();
    }
    [AdminPagePermission(AdminPermission.ManageBonus)]
    [HttpGet("/admin/bonus-codes")]
    public IActionResult BonusCodes()
    {
        return View();
    }
}