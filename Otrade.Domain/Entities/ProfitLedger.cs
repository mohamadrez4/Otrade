using Otrade.Domain.Entities;
using Otrade.Domain.Enums;

public class ProfitLedger
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? SourceUserId { get; set; }

    public string ReferenceId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal? RealCapitalAmount { get; set; }

    public decimal? BonusCapitalAmount { get; set; }

    public decimal? ProfitBaseAmount { get; set; }

    public int? EffectiveRankId { get; set; }

    public Rank? EffectiveRank { get; set; }
    public ProfitType Type { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public User? SourceUser { get; set; }
}