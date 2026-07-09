namespace Otrade.Application.DTOs.Wallet;

public class CreateInternalTransferVerificationRequest
{
    public string Receiver { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Description { get; set; }
}