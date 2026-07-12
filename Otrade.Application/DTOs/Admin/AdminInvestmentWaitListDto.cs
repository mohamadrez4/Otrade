namespace Otrade.Application.DTOs.Admin;

public class AdminInvestmentWaitListDto
{
    public long WaitListId { get; set; }

    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string UserFullName { get; set; } = string.Empty;

    public decimal RequestedAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? NotifiedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? AdminNote { get; set; }
}