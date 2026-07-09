namespace Otrade.Application.DTOs.Wallet;

public class UserPendingWithdrawalDto
{
    public long WithdrawalId { get; set; }

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; } = string.Empty;

    public string Network { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public bool CanCancel { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}