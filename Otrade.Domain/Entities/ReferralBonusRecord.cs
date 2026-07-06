namespace Otrade.Domain.Entities;

public class ReferralBonusRecord
{
    public long BonusId { get; set; }

    public long FromUserId { get; set; }
    public long ToUserId { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; }

    public DateTime CreatedAt { get; set; }

    public User FromUser { get; set; }
    public User ToUser { get; set; }
}