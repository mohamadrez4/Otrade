namespace Otrade.Application.DTOs.Wallet;

public class UserDepositHistoryDto
{
    public long DepositId { get; set; }

    public decimal Amount { get; set; }

    public string TxId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}