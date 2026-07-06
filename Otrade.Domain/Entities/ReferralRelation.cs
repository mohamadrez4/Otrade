namespace Otrade.Domain.Entities;

public class ReferralRelation
{
    public long Id { get; set; }

    public long AncestorId { get; set; }
    public long DescendantId { get; set; }

    public int Depth { get; set; }

    public User Ancestor { get; set; }
    public User Descendant { get; set; }
}