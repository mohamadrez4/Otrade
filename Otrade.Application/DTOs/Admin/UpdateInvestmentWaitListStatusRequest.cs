namespace Otrade.Application.DTOs.Admin;

public class UpdateInvestmentWaitListStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }
}