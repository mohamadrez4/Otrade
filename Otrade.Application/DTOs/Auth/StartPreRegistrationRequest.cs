namespace Otrade.Application.DTOs.Auth;

public class StartPreRegistrationRequest
{
    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;
}