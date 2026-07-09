namespace Otrade.Application.DTOs.Admin;

public class ApprovePreRegistrationResponse
{
    public long TemporaryRegistrationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public decimal ApprovedAmount { get; set; }

    public string CompletionToken { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime ApprovedAt { get; set; }
}