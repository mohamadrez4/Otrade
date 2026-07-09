namespace Otrade.Application.DTOs.Auth;

public class VerifyPreRegistrationEmailRequest
{
    public long TemporaryRegistrationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}