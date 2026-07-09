namespace Otrade.Application.DTOs.Kyc;

public class KycStatusResponse
{
    public string Status { get; set; } = string.Empty;

    public string? RejectReason { get; set; }

    public DateTime? ReviewedAt { get; set; }
}