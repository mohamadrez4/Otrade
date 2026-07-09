using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class InternalTransferVerification
{
    public long InternalTransferVerificationId { get; set; }

    public long SenderUserId { get; set; }

    public long ReceiverUserId { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public InternalTransferVerificationStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public User SenderUser { get; set; } = null!;

    public User ReceiverUser { get; set; } = null!;
}