using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class Wallet
{
    public long WalletId { get; set; }

    public long UserId { get; set; }

    public WalletType WalletType { get; set; }

    public decimal Balance { get; set; }

    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; }
}