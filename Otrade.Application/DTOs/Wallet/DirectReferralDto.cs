namespace Otrade.Application.DTOs.Wallet;

public class DirectReferralDto
{
    public long UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}