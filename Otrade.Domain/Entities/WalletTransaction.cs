namespace Otrade.Domain.Entities;

public class WalletTransaction
{
    public long TransactionId { get; set; }

    public long UserId { get; set; }
    public long WalletId { get; set; }

    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public TransactionType Type { get; set; }

    public string Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; }
    public Wallet Wallet { get; set; }
}