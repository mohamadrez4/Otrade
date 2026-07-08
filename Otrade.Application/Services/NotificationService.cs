using Otrade.Application.Common.Interfaces;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using System.Net.Http;
using System.Text.RegularExpressions;
using Otrade.Application.Services.Security;
namespace Otrade.Application.Services;

public class NotificationService : INotificationService
{
    private readonly OtradeDbContext _context;
    private readonly SystemSettingService _settingService;
    private readonly IEmailService _emailService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EncryptionService _encryptionService;
    public NotificationService(
        OtradeDbContext context,
        SystemSettingService settingService,
        IEmailService emailService,
        IHttpClientFactory httpClientFactory,
        EncryptionService encryptionService)
    {
        _context = context;
        _settingService = settingService;
        _emailService = emailService;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
    }

    public async Task<bool> NotifyAdminAsync(
        string subject,
        string body)
    {
        var emailResult = false;
        var telegramResult = false;

        var adminEmail = await _settingService.GetValueAsync("ADMIN_EMAIL");

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            emailResult = await SendEmailAsync(adminEmail, subject, body);
        }
        else
        {
            await AddEmailLogAsync(
                toEmail: "ADMIN_EMAIL",
                subject: subject,
                body: body,
                status: "Skipped");
        }

        telegramResult = await SendTelegramToAdminAsync(subject, body);

        return emailResult || telegramResult;
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string body)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return false;

        var emailEnabled = await _settingService.GetBoolAsync("NotificationEmailEnabled");

        if (emailEnabled == false)
        {
            await AddEmailLogAsync(
                toEmail,
                subject,
                body,
                "Skipped");

            return false;
        }

        try
        {
            await _emailService.SendAsync(toEmail, subject, body);

            await AddEmailLogAsync(
                toEmail,
                subject,
                body,
                "Sent",
                sentAt: DateTime.Now);

            return true;
        }
        catch (Exception ex)
        {
            await AddEmailLogAsync(
                toEmail,
                subject,
                body + $"<hr/><pre>{ex.Message}</pre>",
                "Failed");

            return false;
        }
    }

    public async Task<bool> SendTelegramToAdminAsync(
        string subject,
        string body)
    {
        var botEnabled = await _settingService.GetBoolAsync("TelegramBotEnabled");
        var notificationEnabled = await _settingService.GetBoolAsync("NotificationTelegramEnabled");

        if (botEnabled != true || notificationEnabled != true)
        {
            await AddTelegramAuditAsync(
                subject,
                "Skipped",
                "Telegram notification is disabled.");

            return false;
        }

        var botTokenValue = await _settingService.GetValueAsync("TelegramBotToken");
        var chatId = await _settingService.GetValueAsync("TelegramChatId");

        if (string.IsNullOrWhiteSpace(botTokenValue) || string.IsNullOrWhiteSpace(chatId))
        {
            await AddTelegramAuditAsync(
                subject,
                "Skipped",
                "Telegram bot token or chat id is not configured.");

            return false;
        }

        var botToken = _encryptionService.Decrypt(botTokenValue);

        try
        {
            var text = BuildTelegramMessage(subject, body);

            var client = _httpClientFactory.CreateClient();

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["disable_web_page_preview"] = "true"
            });

            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                await AddTelegramAuditAsync(
                    subject,
                    "Failed",
                    $"Telegram API error: {(int)response.StatusCode} - {responseBody}");

                return false;
            }

            await AddTelegramAuditAsync(
                subject,
                "Sent",
                $"Telegram notification sent to ChatId: {chatId}");

            return true;
        }
        catch (Exception ex)
        {
            await AddTelegramAuditAsync(
                subject,
                "Failed",
                ex.Message);

            return false;
        }
    }

    private static string BuildTelegramMessage(
        string subject,
        string body)
    {
        var plainBody = StripHtml(body);

        var text =
            $"Otrade Notification\n\n" +
            $"Subject: {subject}\n\n" +
            plainBody;

        if (text.Length > 3800)
            text = text[..3800] + "\n\n...";

        return text;
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = Regex.Replace(input, "<.*?>", " ");
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    private async Task AddEmailLogAsync(
        string toEmail,
        string subject,
        string body,
        string status,
        DateTime? sentAt = null)
    {
        try
        {
            _context.EmailLogs.Add(new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                Status = status,
                SentAt = sentAt,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }
        catch
        {
            // Notification logging must never break the main business flow.
        }
    }

    private async Task AddTelegramAuditAsync(
        string subject,
        string status,
        string details)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "TelegramNotification",
                Details = $"Subject: {subject}; Status: {status}; Details: {details}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }
        catch
        {
            // Telegram logging must never break the main business flow.
        }
    }
}