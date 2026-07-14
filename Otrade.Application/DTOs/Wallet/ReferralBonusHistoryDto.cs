namespace Otrade.Application.DTOs.Wallet;

public class ReferralBonusHistoryDto
{
    public long BonusId { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string FromUserUid { get; set; } = string.Empty;

    public string FromUserEmail { get; set; } = string.Empty;

    public string FromUserFullName { get; set; } = string.Empty;
}