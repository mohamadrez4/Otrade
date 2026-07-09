namespace Otrade.Application.DTOs.Wallet;

public class ConfirmInternalTransferRequest
{
    public long VerificationId { get; set; }

    public string Code { get; set; } = string.Empty;
}   