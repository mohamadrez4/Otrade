namespace Otrade.Domain.Entities;

public class Deposit
{
    public long DepositId { get; set; }

    public long UserId { get; set; }

    public decimal Amount { get; set; }

    public string TxId { get; set; }

    public DepositStatus Status { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public User User { get; set; }
}