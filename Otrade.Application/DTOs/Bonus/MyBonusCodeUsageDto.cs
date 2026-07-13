namespace Otrade.Application.DTOs.Bonus;

public class MyBonusCodeUsageDto
{
    public long UsageId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? CampaignName { get; set; }

    public decimal RealCapitalAmount { get; set; }

    public decimal BonusCapitalAmount { get; set; }

    public string? AppliedRankName { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}