using Otrade.Application.Common.Interfaces;
using Otrade.Application.Services;
using Otrade.Application.Services.Security;
using System.Net;
using System.Net.Mail;

namespace Otrade.Web.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    private readonly SystemSettingService _settingService;
    private readonly EncryptionService _encryptionService;
    public EmailService(IConfiguration config, SystemSettingService systemSettingService, EncryptionService encryptionService)
    {
        _config = config;
        _settingService = systemSettingService;
        _encryptionService = encryptionService;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var settings = await _settingService.GetEmailSettingAsync();
        var smtpHost = settings["SmtpHost"];
        var smtpPort = int.Parse(settings["SmtpPort"]!);
        var username = settings["Username"];
        var password = settings["Password"];
        password = _encryptionService.Decrypt(password!);
        var fromEmail = settings["Username"];
        var fromName = settings["FromName"];

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(fromEmail),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mail.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            throw new Exception($"Email send failed: {ex.Message}", ex);
        }
    }
}