namespace Otrade.Application.DTOs.Wallet;

public class ConfirmWithdrawalRequest
{
    public long VerificationId { get; set; }

    public string Code { get; set; } = string.Empty;
}