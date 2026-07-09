namespace Otrade.Application.DTOs.Auth;

public class SubmitPreRegistrationDepositResponse
{
    public long TemporaryRegistrationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string TxId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string TrackingToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}