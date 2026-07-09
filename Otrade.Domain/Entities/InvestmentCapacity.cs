namespace Otrade.Domain.Entities;

public class InvestmentCapacity
{
    public long CapacityId { get; set; }

    public DateTime MonthStart { get; set; }

    public decimal TotalCapacity { get; set; }

    public decimal UsedCapacity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal RemainingCapacity => TotalCapacity - UsedCapacity;
}