using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class BonusCodeUsage
{
    public long UsageId { get; set; }

    public long BonusCodeId { get; set; }

    public long UserId { get; set; }

    public decimal RealCapitalAmount { get; set; }

    public decimal BonusCapitalAmount { get; set; }

    public int? AppliedRankId { get; set; }

    public BonusCodeUsageStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }

    public BonusCode? BonusCode { get; set; }

    public User? User { get; set; }

    public Rank? AppliedRank { get; set; }
}