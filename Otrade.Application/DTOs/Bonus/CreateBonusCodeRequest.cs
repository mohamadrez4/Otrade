namespace Otrade.Application.DTOs.Bonus;

public class CreateBonusCodeRequest
{
    public string Code { get; set; } = string.Empty;

    public string? CampaignName { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSingleUse { get; set; } = true;

    public int MaxUsageCount { get; set; } = 1;

    public DateTime? ExpiresAt { get; set; }

    public string BonusType { get; set; } = string.Empty;

    public decimal? BonusCapitalPercent { get; set; }

    public int? BonusRankId { get; set; }
}