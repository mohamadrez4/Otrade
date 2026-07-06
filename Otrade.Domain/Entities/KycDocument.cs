using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class KycDocument
{
    public long DocumentId { get; set; }

    public long UserId { get; set; }

    public KycDocumentType DocumentType { get; set; } // NationalID / Selfie

    public string FilePath { get; set; }

    public KycStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; }
}