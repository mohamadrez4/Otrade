namespace Otrade.Application.DTOs.Admin;

public class PendingKycDto
{
    public long DocumentId { get; set; }

    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}