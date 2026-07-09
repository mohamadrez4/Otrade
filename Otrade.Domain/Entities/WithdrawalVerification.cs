using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class WithdrawalVerification
{
    public long WithdrawalVerificationId { get; set; }

    public long UserId { get; set; }

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; } = string.Empty;

    public string Network { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public WithdrawalVerificationStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public User User { get; set; } = null!;
}