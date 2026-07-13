namespace Otrade.Application.DTOs.Admin;

public class PreRegistrationPendingDto
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public long? SponsorId { get; set; }

    public string? SponsorEmail { get; set; }

    public decimal? DeclaredAmount { get; set; }

    public string? DepositTxId { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? SiteWalletAddress { get; set; }

    public string? Network { get; set; }
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}