namespace Otrade.Application.DTOs.Wallet;

public class CurrentInvestmentCapacityResponse
{
    public bool IsConfigured { get; set; }

    public bool IsActive { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public decimal RemainingCapacity { get; set; }
}