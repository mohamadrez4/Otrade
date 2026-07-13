namespace Otrade.Application.DTOs.Bonus;

public class BonusCodeDto
{
    public long BonusCodeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? CampaignName { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsSingleUse { get; set; }

    public int MaxUsageCount { get; set; }

    public int UsedCount { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string BonusType { get; set; } = string.Empty;

    public decimal? BonusCapitalPercent { get; set; }

    public int? BonusRankId { get; set; }

    public string? BonusRankName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}