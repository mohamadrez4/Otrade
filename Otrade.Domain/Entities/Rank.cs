namespace Otrade.Domain.Entities;

public class Rank
{
    public int RankId { get; set; }

    public string Name { get; set; }

    public decimal RequiredVolume { get; set; }

    public decimal MonthlyProfitPercent { get; set; }
    public decimal DailyProfitPercent { get; set; }

    public decimal ReferralProfitPercent { get; set; }
    public decimal MainToInvestPercent { get; set; }

    public int SortOrder { get; set; }
}