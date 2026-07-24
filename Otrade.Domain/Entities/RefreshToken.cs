namespace Otrade.Domain.Entities;

public class RefreshToken
{
    public long RefreshTokenId { get; set; }

    public long UserId { get; set; }


    public string TokenHash { get; set; }
        = string.Empty;


    public int TokenVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string CreatedByIp { get; set; }
        = string.Empty;

    public string? LastUsedByIp { get; set; }

    public string UserAgent { get; set; }
        = string.Empty;

    public User User { get; set; }
        = null!;
}