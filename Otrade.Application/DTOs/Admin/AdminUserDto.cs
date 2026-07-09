public class AdminUserDto
{
    public long UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public RankDto? CurrentRank { get; set; }

    public string KycStatus { get; set; } = string.Empty;

    public UserWalletsDto Wallets { get; set; } = new();

    public string? SponsorEmail { get; set; }
    public string? SponsorFullName { get; set; }

    public string? SponsorReferralCode { get; set; }
}

public class RankDto
{
    public int RankId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class UserWalletsDto
{
    public decimal Main { get; set; }

    public decimal Invest { get; set; }

    public decimal Profit { get; set; }

    public decimal Referral { get; set; }
}