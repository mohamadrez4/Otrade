namespace Otrade.Application.DTOs.Admin;

public class WalletBalanceSnapshotDto
{
    public long SnapshotId { get; set; }

    public DateTime SnapshotDate { get; set; }

    public decimal TotalMainWallet { get; set; }

    public decimal TotalInvestWallet { get; set; }

    public decimal TotalProfitWallet { get; set; }

    public decimal TotalReferralWallet { get; set; }

    public decimal TotalAssets { get; set; }

    public int TotalWallets { get; set; }

    public int UsersWithBalance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}