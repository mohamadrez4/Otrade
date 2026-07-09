namespace Otrade.Application.DTOs.Wallet;

public class CreateInternalTransferVerificationResponse
{
    public long VerificationId { get; set; }

    public string ReceiverUid { get; set; } = string.Empty;

    public string ReceiverEmail { get; set; } = string.Empty;

    public string ReceiverFullName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int ExpiresInMinutes { get; set; }
}