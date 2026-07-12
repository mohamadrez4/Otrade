namespace Otrade.Application.DTOs.Wallet;

public class MyInvestmentWaitListDto
{
    public long WaitListId { get; set; }

    public decimal RequestedAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? NotifiedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }
}