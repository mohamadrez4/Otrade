namespace Otrade.Application.DTOs.Wallet;

public class WithdrawalVerificationResponse
{
    public long VerificationId { get; set; }

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; }
        = string.Empty;

    public string Network { get; set; }
        = string.Empty;

    public int ExpiresInMinutes { get; set; }

    /*
     * Email یا Totp
     */
    public string VerificationMethod { get; set; }
        = string.Empty;

    public bool RequiresEmailCode { get; set; }
}