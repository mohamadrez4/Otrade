using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class BonusCode
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

    public BonusCodeType BonusType { get; set; }

    public decimal? BonusCapitalPercent { get; set; }

    public int? BonusRankId { get; set; }

    public long? CreatedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Rank? BonusRank { get; set; }

    public User? CreatedByAdmin { get; set; }

    public ICollection<BonusCodeUsage> Usages { get; set; }
        = new List<BonusCodeUsage>();
}