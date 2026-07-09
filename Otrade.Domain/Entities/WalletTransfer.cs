namespace Otrade.Domain.Entities;

public class WalletTransfer
{
    public long TransferId { get; set; }

    public long FromWalletId { get; set; }

    public long ToWalletId { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public Wallet FromWallet { get; set; } = null!;

    public Wallet ToWallet { get; set; } = null!;
}