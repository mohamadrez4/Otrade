namespace Otrade.Application.Common.Interfaces;

public interface INotificationService
{
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string body);

    Task<bool> NotifyAdminAsync(
        string subject,
        string body);

    Task<bool> SendTelegramToAdminAsync(
        string subject,
        string body);
}