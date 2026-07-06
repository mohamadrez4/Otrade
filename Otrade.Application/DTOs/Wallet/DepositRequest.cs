namespace Otrade.Application.DTOs.Wallet;

public class DepositRequest
{
    public decimal Amount { get; set; }

    public string TxId { get; set; }
}