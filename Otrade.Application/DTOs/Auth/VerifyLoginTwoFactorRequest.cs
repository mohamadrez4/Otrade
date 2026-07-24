namespace Otrade.Application.DTOs.Auth;

public class VerifyLoginTwoFactorRequest
{
    public string ChallengeToken { get; set; }
        = string.Empty;

    /*
     * کد 6 رقمی Google Authenticator
     * یا Recovery Code استفاده‌نشده.
     */
    public string Code { get; set; }
        = string.Empty;
}