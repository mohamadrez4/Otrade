namespace Otrade.Domain.Entities;

public class WalletTransfer
{
    public long TransferId { get; set; }

    public long FromWalletId { get; set; }
    public long ToWalletId { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}