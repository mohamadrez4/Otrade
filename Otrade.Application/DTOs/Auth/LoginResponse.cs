namespace Otrade.Application.DTOs.Auth;

public class LoginResponse
{
    public long UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool IsOwner { get; set; }

    public bool EmailConfirmed { get; set; }

    public string KycStatus { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public bool RequiresTwoFactor { get; set; }

    public string ChallengeToken { get; set; }
        = string.Empty;

    public DateTime? ChallengeExpiresAt { get; set; }
}