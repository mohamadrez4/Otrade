namespace Otrade.Application.DTOs.Auth;

public class SubmitPreRegistrationDepositRequest
{
    public long TemporaryRegistrationId { get; set; }

    public decimal Amount { get; set; }

    public string TxId { get; set; } = string.Empty;
}