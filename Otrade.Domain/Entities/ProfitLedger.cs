using Otrade.Domain.Entities;

public class ProfitLedger
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string ReferenceId { get; set; } // جلوگیری از دوباره‌کاری

    public decimal Amount { get; set; }

    public ProfitType Type { get; set; } // Investment / Referral

    public DateTime CreatedAt { get; set; }
    public User User { get; set; }
}