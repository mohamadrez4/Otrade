namespace Otrade.Domain.Entities;

public class TwoFactorLoginChallenge
{
    public long ChallengeId { get; set; }

    public long UserId { get; set; }

    /*
     * خود Challenge Token ذخیره نمی‌شود.
     * فقط SHA-256 آن در دیتابیس قرار می‌گیرد.
     */
    public string TokenHash { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}