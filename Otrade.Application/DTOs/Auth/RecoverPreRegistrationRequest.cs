namespace Otrade.Application.DTOs.Auth;

public class RecoverPreRegistrationRequest
{
    public string Email { get; set; } = string.Empty;

    public string TxId { get; set; } = string.Empty;
}