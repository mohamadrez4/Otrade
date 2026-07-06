namespace Otrade.Domain.Entities;

public class RankHistory
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public int? OldRankId { get; set; }
    public int NewRankId { get; set; }

    public decimal Volume { get; set; }

    public DateTime CreatedAt { get; set; }
}