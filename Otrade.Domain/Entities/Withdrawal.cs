namespace Otrade.Domain.Entities;

public class Withdrawal
{
    public long WithdrawalId { get; set; }

    public long UserId { get; set; }

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; }
    public string Network { get; set; }

    public DepositStatus Status { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public User User { get; set; }
}