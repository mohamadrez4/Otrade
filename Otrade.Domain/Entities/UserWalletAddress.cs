namespace Otrade.Domain.Entities;

public class UserWalletAddress
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string? Address { get; set; }

    public string? Network { get; set; }

    public User User { get; set; }
}