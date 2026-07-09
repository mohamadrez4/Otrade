namespace Otrade.Application.DTOs.Admin;

public class AdminWalletSummaryResponse
{
    public decimal TotalAssets { get; set; }

    public decimal TotalMainWallet { get; set; }

    public decimal TotalInvestWallet { get; set; }

    public decimal TotalProfitWallet { get; set; }

    public decimal TotalReferralWallet { get; set; }

    public int TotalWallets { get; set; }

    public int UsersWithBalance { get; set; }
}