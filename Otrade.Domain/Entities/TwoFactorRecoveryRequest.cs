using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class TwoFactorRecoveryRequest
{
    public long TwoFactorRecoveryRequestId { get; set; }

    public long UserId { get; set; }

    /*
     * Public token is never stored directly.
     * Only its SHA-256 hash is stored in the database.
     */
    public string PublicTokenHash { get; set; }
        = string.Empty;

    /*
     * Email verification code is stored as BCrypt hash.
     */
    public string EmailCodeHash { get; set; }
        = string.Empty;

    public int Attempts { get; set; }

    public TwoFactorRecoveryRequestStatus Status { get; set; }

    public string UserDescription { get; set; }
        = string.Empty;

    public string? AdminNote { get; set; }

    public long? ReviewedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime EmailCodeExpiresAt { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public User User { get; set; }
        = null!;

    public User? ReviewedByAdmin { get; set; }
}