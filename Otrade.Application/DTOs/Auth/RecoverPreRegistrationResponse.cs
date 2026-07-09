namespace Otrade.Application.DTOs.Auth;

public class RecoverPreRegistrationResponse
{
    public string TrackingToken { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool CanContinueWaiting { get; set; }

    public bool IsApproved { get; set; }

    public bool IsRejected { get; set; }

    public bool IsExpired { get; set; }

    public string Message { get; set; } = string.Empty;
}