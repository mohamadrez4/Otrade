namespace Otrade.Application.DTOs.Admin;

public class AdminAccessDto
{
    public bool IsAdmin { get; set; }

    public bool IsOwner { get; set; }

    public string AdminRole { get; set; } = "User";

    public bool ManageUsers { get; set; }

    public bool ManageSettings { get; set; }

    public bool ManageDeposits { get; set; }

    public bool ManageWithdrawals { get; set; }

    public bool ManageKyc { get; set; }

    public bool ViewReports { get; set; }

    public bool ManageRanks { get; set; }

    public bool ManageTickets { get; set; }

    public bool ManageHangfire { get; set; }

    public bool ManageAdminRoles { get; set; }
}