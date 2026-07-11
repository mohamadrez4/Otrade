using Otrade.Domain.Enums;

namespace Otrade.Domain.Entities;

public class User
{
    public long UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public long? SponsorId { get; set; }

    public User? Sponsor { get; set; }

    public int? CurrentRankId { get; set; }

    public bool EmailConfirmed { get; set; }

    public bool IsOwner { get; set; }

    public bool IsAdmin { get; set; }
    public AdminRole? AdminRole { get; set; }
    public KycStatus KycStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public ICollection<Wallet> Wallets { get; set; }
        = new List<Wallet>();
}