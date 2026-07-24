namespace Otrade.Application.DTOs.Security;

public class TwoFactorStatusResponse
{
    public bool IsEnabled { get; set; }

    public DateTime? EnabledAt { get; set; }

    public bool HasPendingSetup { get; set; }

    public DateTime? SetupExpiresAt { get; set; }

    public int RecoveryCodesRemaining { get; set; }
}

public class TwoFactorSetupResponse
{
    /*
     * تصویر QR به‌شکل Data URL برگردانده می‌شود
     * و مستقیماً داخل src تگ img قرار می‌گیرد.
     */
    public string QrCodeDataUrl { get; set; }
        = string.Empty;

    /*
     * کلید دستی فقط هنگام Setup به کاربر نمایش داده می‌شود.
     */
    public string ManualEntryKey { get; set; }
        = string.Empty;

    public string Issuer { get; set; }
        = string.Empty;

    public string AccountName { get; set; }
        = string.Empty;

    public DateTime ExpiresAt { get; set; }
}

public class VerifyTwoFactorSetupRequest
{
    public string Code { get; set; }
        = string.Empty;
}

public class TwoFactorEnableResponse
{
    public bool IsEnabled { get; set; }

    public DateTime EnabledAt { get; set; }

    /*
     * این کدها فقط یک‌بار به کاربر نمایش داده می‌شوند.
     */
    public List<string> RecoveryCodes { get; set; }
        = new();
}

public class DisableTwoFactorRequest
{
    public string Password { get; set; }
        = string.Empty;

    /*
     * می‌تواند TOTP شش‌رقمی یا Recovery Code باشد.
     */
    public string Code { get; set; }
        = string.Empty;
}

public class RegenerateRecoveryCodesRequest
{
    public string Code { get; set; }
        = string.Empty;
}

public class RecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; }
        = new();
}
public class TwoFactorLoginChallengeResponse
{
    public string ChallengeToken { get; set; }
        = string.Empty;

    public DateTime ExpiresAt { get; set; }
}