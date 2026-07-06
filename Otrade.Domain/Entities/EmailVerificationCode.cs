namespace Otrade.Domain.Entities;

public class EmailVerificationCode
{
    public long VerificationId { get; set; }

    public long UserId { get; set; }

    public string Code { get; set; } = string.Empty;

    public bool IsUsed { get; set; }

    public DateTime ExpireAt { get; set; }

    public DateTime CreatedAt { get; set; }
}