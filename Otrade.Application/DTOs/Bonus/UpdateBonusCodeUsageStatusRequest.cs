namespace Otrade.Application.DTOs.Bonus;

public class UpdateBonusCodeUsageStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }
}