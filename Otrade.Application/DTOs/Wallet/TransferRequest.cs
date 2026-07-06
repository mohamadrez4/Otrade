namespace Otrade.Application.DTOs.Wallet;

public class TransferRequest
{
    public long FromWalletId { get; set; }

    public long ToWalletId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}