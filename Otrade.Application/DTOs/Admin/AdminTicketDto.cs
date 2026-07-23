namespace Otrade.Application.DTOs.Admin;

public class AdminTicketDto
{
    public long TicketId { get; set; }

    /*
     * برای سازگاری با API قبلی باقی می‌ماند.
     * در پنل ادمین دیگر به‌عنوان نام کاربر نمایش داده نمی‌شود.
     */
    public string UserEmail { get; set; } = string.Empty;

    public string UserFullName { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public List<AdminTicketMessageDto> Messages { get; set; } = new();
}

public class AdminTicketMessageDto
{
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public bool IsSupport { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}