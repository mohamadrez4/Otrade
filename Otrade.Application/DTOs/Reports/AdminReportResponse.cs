namespace Otrade.Application.DTOs.Reports;

public class AdminReportResponse
{
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalInvestmentProfits { get; set; }
    public decimal TotalReferralProfits { get; set; }
    public decimal TotalMainInvestBonuses { get; set; }

    public int PendingDepositsCount { get; set; }
    public int PendingWithdrawalsCount { get; set; }
    public int PendingPreRegistrationsCount { get; set; }
    public int TotalUsers { get; set; }
}