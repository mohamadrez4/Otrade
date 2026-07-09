namespace Otrade.Application.DTOs.Admin;

public class InvestmentCapacityDto
{
    public long CapacityId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public DateTime MonthStart { get; set; }

    public decimal TotalCapacity { get; set; }

    public decimal UsedCapacity { get; set; }

    public decimal RemainingCapacity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}