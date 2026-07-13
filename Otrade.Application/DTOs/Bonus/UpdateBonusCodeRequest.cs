using Otrade.Application.DTOs.Bonus;

namespace Otrade.Application.DTOs.Bonus;

public class UpdateBonusCodeRequest
{
    public string? CampaignName { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsSingleUse { get; set; }

    public int MaxUsageCount { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string BonusType { get; set; } = string.Empty;

    public decimal? BonusCapitalPercent { get; set; }

    public int? BonusRankId { get; set; }
}