namespace Otrade.Application.DTOs.Wallet;

public class UserProfitHistoryDto
{
    public long Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string ReferenceId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal? RealCapitalAmount { get; set; }

    public decimal? BonusCapitalAmount { get; set; }

    public decimal? ProfitBaseAmount { get; set; }

    public string? EffectiveRankName { get; set; }

    public string? SourceUserUid { get; set; }

    public string? SourceUserEmail { get; set; }

    public string? SourceUserFullName { get; set; }
}