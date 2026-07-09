namespace Otrade.Application.DTOs.Auth;

public class VerifyPreRegistrationEmailResponse
{
    public long TemporaryRegistrationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    public string Status { get; set; } = string.Empty;
}