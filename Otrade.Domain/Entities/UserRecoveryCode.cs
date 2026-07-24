namespace Otrade.Domain.Entities;

public class UserRecoveryCode
{
    public long UserRecoveryCodeId { get; set; }

    public long UserId { get; set; }

    /*
     * Recovery Code به‌صورت Hash ذخیره می‌شود.
     * متن اصلی فقط یک‌بار هنگام فعال‌سازی به کاربر نشان داده می‌شود.
     */
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}