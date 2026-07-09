namespace Otrade.Application.DTOs.Dashboard;
using Otrade.Application.DTOs.Wallet;
public class DashboardResponse
{
    public decimal TotalAssets { get; set; }

    public string CurrentRank { get; set; } = string.Empty;

    public decimal NetworkVolume { get; set; }

    public string? NextRank { get; set; }

    public decimal RequiredForNextRank { get; set; }

    public decimal NextRankProgressPercent { get; set; }
    public CurrentInvestmentCapacityResponse? CurrentInvestmentCapacity { get; set; }
    public List<WalletBalanceDto> Wallets { get; set; } = new();
}

public class WalletBalanceDto
{
    public long WalletId { get; set; }
    public string WalletType { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public decimal PercentOfTotal { get; set; }
}