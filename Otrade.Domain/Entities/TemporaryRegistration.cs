using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class TemporaryRegistration
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public long? SponsorId { get; set; }

    public User? Sponsor { get; set; }

    public decimal? DeclaredAmount { get; set; }

    public string? DepositTxId { get; set; }
    public string? SiteWalletAddress { get; set; }

    public string? Network { get; set; }
    public decimal? ApprovedAmount { get; set; }

    public long? ApprovedByUserId { get; set; }

    public User? ApprovedByUser { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? CompletedUserId { get; set; }

    public User? CompletedUser { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TemporaryRegistrationStatus Status { get; set; }

    public string? RejectReason { get; set; }

    public string? EmailVerificationCode { get; set; }

    public DateTime? EmailVerificationExpireAt { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public string? RecoveryVerificationCode { get; set; }

    public DateTime? RecoveryVerificationExpireAt { get; set; }

    public string? TrackingToken { get; set; }

    public string? CompletionToken { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}