namespace Otrade.Application.DTOs.Ticket;

public class TicketResponse
{
    public long TicketId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<TicketMessageDto> Messages { get; set; } = new();
}

public class TicketMessageDto
{
    /*
     * برای سازگاری با نسخه‌های قبلی API نگه داشته می‌شود،
     * اما دیگر در رابط کاربری نمایش داده نخواهد شد.
     */
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    /*
     * اگر پیام توسط ادمین یا مالک سیستم ارسال شده باشد،
     * Frontend عنوان Support Team را نمایش می‌دهد.
     */
    public bool IsSupport { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}