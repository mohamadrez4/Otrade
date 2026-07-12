using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class InvestmentWaitListEntry
{
    public long WaitListId { get; set; }

    public long UserId { get; set; }

    public decimal RequestedAmount { get; set; }

    public InvestmentWaitListStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? NotifiedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }

    public User? User { get; set; }
}