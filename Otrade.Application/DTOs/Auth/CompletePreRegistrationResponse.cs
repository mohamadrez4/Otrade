namespace Otrade.Application.DTOs.Auth;

public class CompletePreRegistrationResponse
{
    public long UserId { get; set; }

    public long TemporaryRegistrationId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public decimal MainWalletBalance { get; set; }

    public decimal InvestWalletBalance { get; set; }

    public decimal ApprovedAmount { get; set; }

    public string Token { get; set; } = string.Empty;
}