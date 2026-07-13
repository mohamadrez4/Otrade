namespace Otrade.Application.DTOs.Bonus;

public class BonusCodeUsageDto
{
    public long UsageId { get; set; }

    public long BonusCodeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? CampaignName { get; set; }

    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string UserFullName { get; set; } = string.Empty;

    public decimal RealCapitalAmount { get; set; }

    public decimal BonusCapitalAmount { get; set; }

    public int? AppliedRankId { get; set; }

    public string? AppliedRankName { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }
}