namespace Otrade.Application.DTOs.Admin;

public class SaveInvestmentCapacityRequest
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal TotalCapacity { get; set; }

    public bool IsActive { get; set; } = true;
}