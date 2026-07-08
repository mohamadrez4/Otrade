namespace Otrade.Application.DTOs.Auth;

public class StartPreRegistrationResponse
{
    public long TemporaryRegistrationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public long? SponsorId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}