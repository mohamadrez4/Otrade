namespace Otrade.Application.DTOs.Auth;

public class PreRegistrationWaitStatusResponse
{
    public string Status { get; set; } = string.Empty;

    public bool IsPending { get; set; }

    public bool IsApproved { get; set; }

    public bool IsRejected { get; set; }

    public bool IsExpired { get; set; }

    public bool IsWaitOver { get; set; }

    public string? CompletionToken { get; set; }

    public decimal? ApprovedAmount { get; set; }

    public string? RejectReason { get; set; }

    public int RemainingSeconds { get; set; }

    public string Message { get; set; } = string.Empty;
}