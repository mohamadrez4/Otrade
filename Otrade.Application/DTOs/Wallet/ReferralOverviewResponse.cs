using Otrade.Application.DTOs.Common;

namespace Otrade.Application.DTOs.Wallet;

public class ReferralOverviewResponse
{
    public string ReferralCode { get; set; } = string.Empty;

    public string CurrentRank { get; set; } = string.Empty;

    public decimal ReferralProfitPercent { get; set; }

    public decimal MainToInvestPercent { get; set; }

    public decimal ReferralWalletBalance { get; set; }

    public int TotalReferrals { get; set; }

    public decimal TotalBonus { get; set; }

    public PagedResponse<DirectReferralDto> Referrals { get; set; } = new();

    public PagedResponse<ReferralBonusHistoryDto> Bonuses { get; set; } = new();
}