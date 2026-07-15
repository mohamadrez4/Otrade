using Otrade.Application.Common.Interfaces;
using Otrade.Application.Services.Security;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
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
                ["parse_mode"] = "HTML",
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
        var normalizedSubject = string.IsNullOrWhiteSpace(subject)
            ? "Otrade Notification"
            : subject.Trim();

        var plainBody = ConvertEmailHtmlToTelegramText(
            body,
            normalizedSubject);

        // محدودیت Telegram برای sendMessage حدود 4096 کاراکتر است.
        // قبل از HTML Encode کوتاه می‌کنیم تا وسط entity بریده نشود.
        if (plainBody.Length > 3300)
        {
            plainBody = plainBody[..3300].TrimEnd() + "\n\n...";
        }

        var safeSubject = WebUtility.HtmlEncode(normalizedSubject);
        var safeBody = WebUtility.HtmlEncode(plainBody);

        return
            "🔔 <b>Otrade Notification</b>\n\n" +
            $"📌 <b>Subject:</b> {safeSubject}\n\n" +
            safeBody;
    }

    private static string ConvertEmailHtmlToTelegramText(
        string? html,
        string subject)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        const RegexOptions options =
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant;

        var text = html;

        // بخش‌هایی که هیچ ارزش نمایشی در Telegram ندارند.
        text = Regex.Replace(
            text,
            @"<!doctype[^>]*>",
            string.Empty,
            options);

        text = Regex.Replace(
            text,
            @"<head\b[^>]*>.*?</head>",
            string.Empty,
            options);

        text = Regex.Replace(
            text,
            @"<(?:script|style)\b[^>]*>.*?</(?:script|style)>",
            string.Empty,
            options);

        text = Regex.Replace(
            text,
            @"<!--.*?-->",
            string.Empty,
            options);

        // حذف preheader مخفی ایمیل.
        text = Regex.Replace(
            text,
            @"<div\b(?=[^>]*\bstyle\s*=\s*['""][^'""]*display\s*:\s*none)[^>]*>.*?</div>",
            string.Empty,
            options);

        // حذف تصاویر و لوگو از متن Telegram.
        text = Regex.Replace(
            text,
            @"<img\b[^>]*>",
            string.Empty,
            options);

        /*
            تبدیل ردیف‌های DetailTable ایمیل از:

            User        test@example.com
            Amount      100 USDT

            به:

            User: test@example.com
            Amount: 100 USDT
        */
        var detailRowPattern =
            @"<tr\b[^>]*>\s*" +
            @"<td\b[^>]*>(?<label>(?:(?!<td\b|</td>).)*)</td>\s*" +
            @"<td\b[^>]*>(?<value>(?:(?!<td\b|</td>).)*)</td>\s*" +
            @"</tr>";

        text = Regex.Replace(
            text,
            detailRowPattern,
            match =>
            {
                var label = StripInlineHtml(
                    match.Groups["label"].Value);

                var value = StripInlineHtml(
                    match.Groups["value"].Value);

                if (string.IsNullOrWhiteSpace(label) &&
                    string.IsNullOrWhiteSpace(value))
                {
                    return "\n";
                }

                if (string.IsNullOrWhiteSpace(label))
                    return $"\n{value}\n";

                if (string.IsNullOrWhiteSpace(value))
                    return $"\n{label}\n";

                return $"\n{label}: {value}\n";
            },
            options);

        // نگه‌داشتن دکمه ایمیل به‌صورت متن و لینک قابل کلیک.
        text = Regex.Replace(
            text,
            @"<a\b[^>]*\bhref\s*=\s*['""](?<url>[^'""]+)['""][^>]*>(?<label>.*?)</a>",
            match =>
            {
                var label = StripInlineHtml(
                    match.Groups["label"].Value);

                var url = WebUtility.HtmlDecode(
                    match.Groups["url"].Value).Trim();

                if (string.IsNullOrWhiteSpace(label))
                    return url;

                if (string.IsNullOrWhiteSpace(url) ||
                    url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    return label;
                }

                return $"{label}: {url}";
            },
            options);

        // تگ‌های block به خط جدید تبدیل شوند.
        text = Regex.Replace(
            text,
            @"<(?:br|/p|/div|/h[1-6]|/li|/tr|/table)\b[^>]*>",
            "\n",
            options);

        // سایر تگ‌ها حذف شوند؛ [^>] تگ‌های چندخطی را هم پوشش می‌دهد.
        text = Regex.Replace(
            text,
            @"<[^>]+>",
            " ",
            options);

        text = WebUtility.HtmlDecode(text)
            .Replace('\u00A0', ' ')
            .Replace("\r", string.Empty);

        var sourceLines = text.Split('\n');

        var resultLines = new List<string>();
        string? previousLine = null;

        foreach (var sourceLine in sourceLines)
        {
            var line = Regex.Replace(
                    sourceLine,
                    @"[ \t]+",
                    " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // حذف موارد تکراری و تزئینی قالب ایمیل.
            if (line.Equals(
                    subject,
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Otrade",
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Online Trading Room",
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Success",
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Action Required",
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Important",
                    StringComparison.OrdinalIgnoreCase) ||
                line.Equals(
                    "Otrade Update",
                    StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith(
                    "© ",
                    StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith(
                    "This is an automated message",
                    StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith(
                    "Need help?",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // جلوگیری از نمایش خطوط تکراری پشت سر هم.
            if (line.Equals(
                    previousLine,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resultLines.Add(line);
            previousLine = line;
        }

        return string.Join("\n", resultLines).Trim();
    }

    private static string StripInlineHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = Regex.Replace(
            value,
            @"<br\s*/?>",
            " ",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant);

        text = Regex.Replace(
            text,
            @"<[^>]+>",
            " ",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant);

        text = WebUtility.HtmlDecode(text)
            .Replace('\u00A0', ' ');

        return Regex.Replace(text, @"\s+", " ").Trim();
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