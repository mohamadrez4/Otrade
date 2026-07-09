namespace Otrade.Application.DTOs.Auth;

public class VerifyRecoverPreRegistrationRequest
{
    public string Email { get; set; } = string.Empty;

    public string TxId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}