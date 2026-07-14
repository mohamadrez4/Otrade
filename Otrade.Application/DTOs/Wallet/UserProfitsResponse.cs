using Otrade.Application.DTOs.Common;

namespace Otrade.Application.DTOs.Wallet;

public class UserProfitsResponse
{
    public decimal TotalProfit { get; set; }

    public decimal InvestmentProfit { get; set; }

    public decimal ReferralProfit { get; set; }

    public PagedResponse<UserProfitHistoryDto> History { get; set; } = new();
}