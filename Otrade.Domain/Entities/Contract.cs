namespace Otrade.Domain.Entities;

public class Contract
{
    public long ContractId { get; set; }

    public long UserId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public ContractStatus Status { get; set; }

    public User User { get; set; }
}