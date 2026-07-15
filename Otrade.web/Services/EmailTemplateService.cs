using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using Otrade.Application.Common.Interfaces;
using System.Globalization;
using System.Net;

namespace Otrade.Web.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private const string BrandName = "Otrade";
    private const string BrandTagline = "Online Trading Room";

    private readonly string _logoUrl;
    private readonly string _panelUrl;
    private readonly string _supportEmail;

    public EmailTemplateService(IConfiguration configuration)
    {
        _logoUrl =
            configuration["EmailBranding:LogoUrl"]?.Trim()
            ?? "https://otrhedge.com/images/email/Logo2.png";

        _panelUrl =
            configuration["EmailBranding:PanelUrl"]?.Trim().TrimEnd('/')
            ?? "https://otrhedge.com";

        _supportEmail =
            configuration["EmailBranding:SupportEmail"]?.Trim()
            ?? "otradesupport@gmail.com";
    }

    public string GetVerificationEmail(string code)
    {
        var content =
            VerificationCode(code, "Email verification code") +
            Notice(
                "This verification code expires in 10 minutes. Never share this code with anyone.",
                EmailTone.Info);

        return BuildEmail(
            title: "Welcome to Otrade",
            intro: "Verify your email address to activate your Otrade account.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: "Your Otrade email verification code is ready.",
            buttonText: "Open Otrade",
            buttonUrl: _panelUrl);
    }

    public string GetWithdrawalVerificationEmail(
        decimal amount,
        string walletAddress,
        string network,
        string code,
        int expiresInMinutes)
    {
        var content =
            DetailTable(
                ("Amount", Money(amount)),
                ("Network", network),
                ("Wallet Address", walletAddress)) +
            VerificationCode(code, "Withdrawal verification code") +
            Notice(
                $"This code expires in {expiresInMinutes} minutes. If you did not request this withdrawal, contact support immediately.",
                EmailTone.Danger);

        return BuildEmail(
            title: "Withdrawal Verification",
            intro: "Confirm your withdrawal request from the Main Wallet.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: $"Confirm your {Money(amount)} withdrawal request.",
            buttonText: "Open Withdrawal",
            buttonUrl: PanelLink("/withdrawal"));
    }

    public string GetDepositNotification(string userEmail, decimal amount, string txId)
    {
        var content =
            DetailTable(
                ("User", userEmail),
                ("Amount", Money(amount)),
                ("Transaction Hash", txId)) +
            Notice(
                "A new deposit request is waiting for admin review.",
                EmailTone.Info);

        return BuildEmail(
            title: "New Deposit Request",
            intro: "A user submitted a new deposit request.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: $"New deposit request from {userEmail}.",
            buttonText: "Review Deposit",
            buttonUrl: PanelLink("/admin/deposits"));
    }

    public string GetDepositApprovedEmail(decimal requestedAmount, decimal approvedAmount)
    {
        var content =
            DetailTable(
                ("Requested Amount", Money(requestedAmount)),
                ("Approved Amount", Money(approvedAmount)),
                ("Credited Wallet", "Main Wallet")) +
            Notice(
                "The approved amount has been credited successfully to your Main Wallet.",
                EmailTone.Success);

        return BuildEmail(
            title: "Deposit Approved",
            intro: "Your deposit request has been reviewed and approved.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Your deposit of {Money(approvedAmount)} was approved.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetWithdrawalNotification(
        string userEmail,
        decimal amount,
        string walletAddress)
    {
        var content =
            DetailTable(
                ("User", userEmail),
                ("Amount", Money(amount)),
                ("Wallet Address", walletAddress)) +
            Notice(
                "A new withdrawal request is waiting for admin review.",
                EmailTone.Warning);

        return BuildEmail(
            title: "New Withdrawal Request",
            intro: "A user submitted a withdrawal request.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: $"New withdrawal request from {userEmail}.",
            buttonText: "Review Withdrawal",
            buttonUrl: PanelLink("/admin/withdrawals"));
    }

    public string GetWithdrawalSubmittedEmail(decimal amount, string walletAddress)
    {
        var content =
            DetailTable(
                ("Amount", Money(amount)),
                ("Wallet Address", walletAddress),
                ("Status", "Pending admin review")) +
            Notice(
                "Your request was submitted successfully. You will receive another email after it is reviewed.",
                EmailTone.Info);

        return BuildEmail(
            title: "Withdrawal Request Submitted",
            intro: "Your withdrawal request is now waiting for admin review.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: $"Your withdrawal request for {Money(amount)} was submitted.",
            buttonText: "View Withdrawal History",
            buttonUrl: PanelLink("/withdrawal"));
    }

    public string GetTicketCreatedEmail(string userEmail, string subject)
    {
        var content =
            DetailTable(
                ("User", userEmail),
                ("Subject", subject)) +
            Notice(
                "A new support ticket is waiting for review.",
                EmailTone.Info);

        return BuildEmail(
            title: "New Support Ticket",
            intro: "A user created a new support ticket.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: $"New support ticket: {subject}.",
            buttonText: "Open Tickets",
            buttonUrl: PanelLink("/admin/tickets"));
    }

    public string GetTicketReplyEmail(string ticketSubject, string replyMessage)
    {
        var content =
            DetailTable(("Ticket Subject", ticketSubject)) +
            MessagePanel("Support Reply", replyMessage, EmailTone.Info);

        return BuildEmail(
            title: "Your Ticket Has a New Reply",
            intro: "The Otrade support team replied to your ticket.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: $"New reply for ticket: {ticketSubject}.",
            buttonText: "View Ticket",
            buttonUrl: PanelLink("/tickets"));
    }

    public string GetKycSubmittedAdminEmail(
        string userEmail,
        string userUid,
        string userFullName,
        string uploadedDocuments)
    {
        var content =
            DetailTable(
                ("UID", userUid),
                ("Email", userEmail),
                ("Full Name", userFullName),
                ("Uploaded Documents", uploadedDocuments)) +
            Notice(
                "Please review the submitted documents in the Admin KYC panel.",
                EmailTone.Warning);

        return BuildEmail(
            title: "New KYC Submission",
            intro: "A user submitted KYC documents for verification.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: $"New KYC submission from {userEmail}.",
            buttonText: "Review KYC",
            buttonUrl: PanelLink("/admin/kyc"));
    }

    public string GetKycApprovedEmail()
    {
        var content =
            Notice(
                "Your identity verification has been approved successfully. KYC-restricted features are now available to you.",
                EmailTone.Success);

        return BuildEmail(
            title: "KYC Approved",
            intro: "Your Otrade identity verification is complete.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: "Your Otrade KYC verification was approved.",
            buttonText: "Open Profile",
            buttonUrl: PanelLink("/profile"));
    }

    public string GetKycRejectedEmail(string reason)
    {
        var content =
            MessagePanel("Rejection Reason", reason, EmailTone.Danger) +
            Notice(
                "Please review the reason, prepare valid documents and submit your KYC request again.",
                EmailTone.Warning);

        return BuildEmail(
            title: "KYC Rejected",
            intro: "Your submitted KYC documents could not be approved.",
            contentHtml: content,
            tone: EmailTone.Danger,
            preheader: "Your Otrade KYC verification requires attention.",
            buttonText: "Open Profile",
            buttonUrl: PanelLink("/profile"));
    }

    public string GetWithdrawalApprovedEmail(decimal amount)
    {
        var content =
            DetailTable(
                ("Approved Amount", Money(amount)),
                ("Status", "Approved")) +
            Notice(
                "Your withdrawal request has been approved.",
                EmailTone.Success);

        return BuildEmail(
            title: "Withdrawal Approved",
            intro: "Your withdrawal request was approved successfully.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Your withdrawal of {Money(amount)} was approved.",
            buttonText: "View Withdrawal History",
            buttonUrl: PanelLink("/withdrawal"));
    }

    public string GetWithdrawalRejectedEmail(decimal amount, string reason)
    {
        var content =
            DetailTable(
                ("Amount", Money(amount)),
                ("Status", "Rejected")) +
            MessagePanel("Rejection Reason", reason, EmailTone.Danger);

        return BuildEmail(
            title: "Withdrawal Rejected",
            intro: "Your withdrawal request could not be approved.",
            contentHtml: content,
            tone: EmailTone.Danger,
            preheader: $"Your withdrawal request for {Money(amount)} was rejected.",
            buttonText: "View Withdrawal History",
            buttonUrl: PanelLink("/withdrawal"));
    }

    public string GetWithdrawalCanceledEmail(decimal amount)
    {
        var content =
            DetailTable(
                ("Canceled Amount", Money(amount)),
                ("Status", "Canceled"),
                ("Returned Wallet", "Main Wallet")) +
            Notice(
                "The reserved amount has been returned to your Main Wallet.",
                EmailTone.Success);

        return BuildEmail(
            title: "Withdrawal Canceled",
            intro: "Your withdrawal request has been canceled.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: $"Your withdrawal request for {Money(amount)} was canceled.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetDepositRejectedEmail(decimal? amount, string reason)
    {
        var content =
            DetailTable(
                ("Amount", Money(amount)),
                ("Status", "Rejected")) +
            MessagePanel("Rejection Reason", reason, EmailTone.Danger);

        return BuildEmail(
            title: "Deposit Rejected",
            intro: "Your deposit request could not be approved.",
            contentHtml: content,
            tone: EmailTone.Danger,
            preheader: "Your Otrade deposit request was rejected.",
            buttonText: "View Deposit History",
            buttonUrl: PanelLink("/deposit"));
    }

    public string GetPasswordResetEmail(string code)
    {
        var content =
            VerificationCode(code, "Password reset code") +
            Notice(
                "This code expires in 15 minutes. If you did not request a password reset, you can safely ignore this email.",
                EmailTone.Warning);

        return BuildEmail(
            title: "Password Reset Request",
            intro: "Use the security code below to reset your Otrade password.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: "Your Otrade password reset code is ready.");
    }

    public string GetInternalTransferVerificationEmail(
        decimal amount,
        string receiverDisplay,
        string code,
        int expiresInMinutes)
    {
        var content =
            DetailTable(
                ("Receiver", receiverDisplay),
                ("Amount", Money(amount)),
                ("From Wallet", "Main Wallet")) +
            VerificationCode(code, "Transfer verification code") +
            Notice(
                $"This code expires in {expiresInMinutes} minutes. If you did not request this transfer, contact support immediately.",
                EmailTone.Danger);

        return BuildEmail(
            title: "Internal Transfer Verification",
            intro: "Confirm your internal transfer request.",
            contentHtml: content,
            tone: EmailTone.Warning,
            preheader: $"Confirm your internal transfer of {Money(amount)}.");
    }

    public string GetInternalTransferCompletedEmail(
        decimal amount,
        string receiverDisplay)
    {
        var content =
            DetailTable(
                ("Receiver", receiverDisplay),
                ("Transferred Amount", Money(amount)),
                ("Status", "Completed")) +
            Notice(
                "Your internal transfer has been completed successfully.",
                EmailTone.Success);

        return BuildEmail(
            title: "Internal Transfer Completed",
            intro: "Your transfer was completed successfully.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Your internal transfer of {Money(amount)} was completed.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetInvestmentWaitListJoinedEmail(decimal requestedAmount)
    {
        var content =
            DetailTable(
                ("Requested Amount", Money(requestedAmount)),
                ("Status", "Waiting")) +
            Notice(
                "Your request was added to the investment wait list. We will notify you when capacity becomes available.",
                EmailTone.Info);

        return BuildEmail(
            title: "Investment Wait List",
            intro: "Your investment wait list request was registered.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: $"Your wait list request for {Money(requestedAmount)} was registered.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetInvestmentCapacityAvailableEmail(decimal requestedAmount)
    {
        var content =
            DetailTable(
                ("Requested Amount", Money(requestedAmount)),
                ("Status", "Capacity may be available")) +
            Notice(
                "Log in to your Otrade panel and try transferring funds to your Invest Wallet.",
                EmailTone.Success);

        return BuildEmail(
            title: "Investment Capacity Available",
            intro: "Investment capacity may now be available for your request.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Investment capacity may be available for {Money(requestedAmount)}.",
            buttonText: "Transfer to Invest",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetInvestmentWaitListNotifiedEmail(decimal requestedAmount)
    {
        var content =
            DetailTable(
                ("Requested Amount", Money(requestedAmount)),
                ("Status", "Still on wait list")) +
            Notice(
                "Your request has been reviewed. We will notify you again when investment capacity becomes available.",
                EmailTone.Info);

        return BuildEmail(
            title: "Investment Wait List Update",
            intro: "There is a new update about your investment wait list request.",
            contentHtml: content,
            tone: EmailTone.Info,
            preheader: "Your investment wait list request has been reviewed.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetInvestWalletTransferEmail(
        decimal amount,
        string fromWalletType,
        decimal investWalletBalance)
    {
        var content =
            DetailTable(
                ("From Wallet", fromWalletType),
                ("Transferred Amount", Money(amount)),
                ("Current Invest Balance", Money(investWalletBalance))) +
            Notice(
                "Your Invest Wallet balance has been updated successfully.",
                EmailTone.Success);

        return BuildEmail(
            title: "Investment Wallet Updated",
            intro: "Your transfer to the Invest Wallet was completed.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Your Invest Wallet received {Money(amount)}.",
            buttonText: "View Wallets",
            buttonUrl: PanelLink("/wallets"));
    }

    public string GetBonusCodeAppliedEmail(
        string code,
        decimal bonusCapitalAmount,
        string? appliedRankName)
    {
        var details = new List<(string Label, string Value)>
        {
            ("Bonus Code", code),
            ("Bonus Capital", Money(bonusCapitalAmount))
        };

        if (!string.IsNullOrWhiteSpace(appliedRankName))
        {
            details.Add(("Applied Rank", appliedRankName));
        }

        var content =
            DetailTable(details.ToArray()) +
            Notice(
                "Your bonus code is active. You can review its details and expiration date in the Bonus Codes section.",
                EmailTone.Success);

        return BuildEmail(
            title: "Bonus Code Applied",
            intro: "Your bonus code was applied successfully.",
            contentHtml: content,
            tone: EmailTone.Success,
            preheader: $"Bonus code {code} was applied successfully.",
            buttonText: "View Bonus Codes",
            buttonUrl: PanelLink("/bonus-codes"));
    }

    public string GetBonusUsageStatusChangedEmail(
        string code,
        string status,
        decimal bonusCapitalAmount,
        string? appliedRankName,
        string? adminNote)
    {
        var details = new List<(string Label, string Value)>
        {
            ("Bonus Code", code),
            ("New Status", status),
            ("Bonus Capital", Money(bonusCapitalAmount))
        };

        if (!string.IsNullOrWhiteSpace(appliedRankName))
        {
            details.Add(("Applied Rank", appliedRankName));
        }

        var tone = status.Equals("Expired", StringComparison.OrdinalIgnoreCase)
            ? EmailTone.Warning
            : status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                ? EmailTone.Danger
                : EmailTone.Info;

        var content = DetailTable(details.ToArray());

        if (!string.IsNullOrWhiteSpace(adminNote))
        {
            content += MessagePanel("Admin Note", adminNote, tone);
        }

        content += Notice(
            "This bonus is no longer included in active bonus calculations when its status is not Active.",
            tone);

        return BuildEmail(
            title: "Bonus Status Updated",
            intro: "The Otrade admin team updated one of your bonus records.",
            contentHtml: content,
            tone: tone,
            preheader: $"Bonus code {code} status changed to {status}.",
            buttonText: "View Bonus Codes",
            buttonUrl: PanelLink("/bonus-codes"));
    }

    private string BuildEmail(
        string title,
        string intro,
        string contentHtml,
        EmailTone tone,
        string preheader,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        var theme = GetTheme(tone);

        var buttonHtml =
            string.IsNullOrWhiteSpace(buttonText) || string.IsNullOrWhiteSpace(buttonUrl)
                ? string.Empty
                : $@"
                    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='margin-top:26px;'>
                        <tr>
                            <td align='center'>
                                <a href='{E(buttonUrl)}'
                                   style='display:inline-block;background:#11E9A1;color:#07100D;text-decoration:none;
                                          font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:800;
                                          padding:15px 26px;border-radius:13px;border:1px solid #18F5AC;'>
                                    {E(buttonText)}
                                </a>
                            </td>
                        </tr>
                    </table>";

        return $@"<!doctype html>
<html lang='en'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width,initial-scale=1'>
    <meta name='x-apple-disable-message-reformatting'>
    <title>{E(title)}</title>
</head>
<body style='margin:0;padding:0;background:#070B10;color:#FFFFFF;'>
    <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
        {E(preheader)}
    </div>

    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
           style='width:100%;background:#070B10;margin:0;padding:0;'>
        <tr>
            <td align='center' style='padding:26px 12px;'>
                <table role='presentation' width='640' cellspacing='0' cellpadding='0' border='0'
                       style='width:100%;max-width:640px;background:#0B0F14;border:1px solid #1C2532;
                              border-radius:24px;overflow:hidden;box-shadow:0 24px 70px rgba(0,0,0,.45);'>

                    <tr>
                        <td style='height:6px;background:{theme.Accent};font-size:0;line-height:0;'>&nbsp;</td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:26px 24px 14px;'>
                            <table role='presentation' cellspacing='0' cellpadding='0' border='0'
                                   style='background:#FFFFFF;border-radius:20px;border:1px solid #E5E7EB;'>
                                <tr>
                                    <td align='center' style='padding:14px 20px;'>
                                        <img src='{E(_logoUrl)}'
                                             width='118'
                                             alt='{BrandName} - {BrandTagline}'
                                             style='display:block;width:118px;max-width:118px;height:auto;border:0;outline:none;text-decoration:none;'>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:6px 28px 0;'>
                            <span style='display:inline-block;background:{theme.SoftBackground};color:{theme.Accent};
                                         border:1px solid {theme.Border};border-radius:999px;padding:7px 13px;
                                         font-family:Arial,Helvetica,sans-serif;font-size:12px;font-weight:800;
                                         letter-spacing:.5px;text-transform:uppercase;'>
                                {theme.Label}
                            </span>
                        </td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:18px 30px 0;'>
                            <h1 style='margin:0;color:#FFFFFF;font-family:Arial,Helvetica,sans-serif;
                                       font-size:32px;line-height:1.2;font-weight:900;letter-spacing:-.5px;'>
                                {E(title)}
                            </h1>
                        </td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:12px 34px 0;'>
                            <p style='margin:0;color:#A7B5C7;font-family:Arial,Helvetica,sans-serif;
                                      font-size:18px;line-height:1.7;font-weight:500;'>
                                {E(intro)}
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:28px 26px 8px;'>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                                   style='background:#121821;border:1px solid #1C2532;border-radius:18px;'>
                                <tr>
                                    <td style='padding:22px;'>
                                        {contentHtml}
                                        {buttonHtml}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:24px 28px 8px;'>
                            <p style='margin:0;color:#7F8EA3;font-family:Arial,Helvetica,sans-serif;
                                      font-size:13px;line-height:1.7;'>
                                Need help?
                                <a href='mailto:{E(_supportEmail)}'
                                   style='color:#11E9A1;text-decoration:none;font-weight:700;'>
                                    {E(_supportEmail)}
                                </a>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td align='center' style='padding:0 28px 26px;'>
                            <p style='margin:0;color:#536174;font-family:Arial,Helvetica,sans-serif;
                                      font-size:12px;line-height:1.7;'>
                                © {DateTime.UtcNow.Year} {BrandName}. {BrandTagline}.<br>
                                This is an automated message. Please do not share verification codes.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string DetailTable(params (string Label, string Value)[] items)
    {
        if (items.Length == 0)
        {
            return string.Empty;
        }

        var rows = string.Join(
            string.Empty,
            items.Select(item => $@"
                <tr>
                    <td style='padding:13px 0;border-bottom:1px solid #1C2532;
                               color:#94A3B8;font-family:Arial,Helvetica,sans-serif;
                               font-size:14px;line-height:1.5;font-weight:700;vertical-align:top;'>
                        {E(item.Label)}
                    </td>
                    <td align='right'
                        style='padding:13px 0 13px 18px;border-bottom:1px solid #1C2532;
                               color:#FFFFFF;font-family:Arial,Helvetica,sans-serif;
                               font-size:16px;line-height:1.5;font-weight:800;vertical-align:top;
                               word-break:break-word;overflow-wrap:anywhere;'>
                        {E(item.Value)}
                    </td>
                </tr>"));

        return $@"
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                   style='width:100%;border-collapse:collapse;margin:0 0 18px;'>
                {rows}
            </table>";
    }

    private static string VerificationCode(string code, string label)
    {
        return $@"
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                   style='margin:4px 0 18px;'>
                <tr>
                    <td align='center'
                        style='padding:20px 14px;background:#0B0F14;border:1px solid rgba(17,233,161,.38);
                               border-radius:16px;'>
                        <div style='color:#94A3B8;font-family:Arial,Helvetica,sans-serif;
                                    font-size:13px;font-weight:700;margin-bottom:10px;'>
                            {E(label)}
                        </div>
                        <div style='color:#11E9A1;font-family:Arial,Helvetica,sans-serif;
                                    font-size:36px;line-height:1.1;font-weight:900;letter-spacing:8px;'>
                            {E(code)}
                        </div>
                    </td>
                </tr>
            </table>";
    }

    private static string Notice(string message, EmailTone tone)
    {
        var theme = GetTheme(tone);

        return $@"
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                   style='margin:12px 0 0;'>
                <tr>
                    <td style='padding:15px 16px;background:{theme.SoftBackground};
                               border:1px solid {theme.Border};border-left:4px solid {theme.Accent};
                               border-radius:13px;color:#D8E2EE;font-family:Arial,Helvetica,sans-serif;
                               font-size:15px;line-height:1.7;font-weight:600;'>
                        {Multiline(message)}
                    </td>
                </tr>
            </table>";
    }

    private static string MessagePanel(string label, string message, EmailTone tone)
    {
        var theme = GetTheme(tone);

        return $@"
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                   style='margin:8px 0 18px;'>
                <tr>
                    <td style='padding:17px;background:#0B0F14;border:1px solid {theme.Border};
                               border-radius:14px;'>
                        <div style='margin-bottom:8px;color:{theme.Accent};
                                    font-family:Arial,Helvetica,sans-serif;font-size:13px;
                                    font-weight:800;text-transform:uppercase;letter-spacing:.4px;'>
                            {E(label)}
                        </div>
                        <div style='color:#FFFFFF;font-family:Arial,Helvetica,sans-serif;
                                    font-size:16px;line-height:1.75;font-weight:600;
                                    word-break:break-word;overflow-wrap:anywhere;'>
                            {Multiline(message)}
                        </div>
                    </td>
                </tr>
            </table>";
    }

    private string PanelLink(string path)
    {
        return $"{_panelUrl}/{path.TrimStart('/')}";
    }

    private static string Money(decimal amount)
    {
        return $"{amount.ToString("N2", CultureInfo.InvariantCulture)} USDT";
    }

    private static string Money(decimal? amount)
    {
        return amount.HasValue
            ? Money(amount.Value)
            : "—";
    }

    private static string E(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string Multiline(string? value)
    {
        return E(value)
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>");
    }

    private static EmailTheme GetTheme(EmailTone tone)
    {
        return tone switch
        {
            EmailTone.Success => new EmailTheme(
                Label: "Success",
                Accent: "#11E9A1",
                SoftBackground: "#0F2A23",
                Border: "#185844"),

            EmailTone.Warning => new EmailTheme(
                Label: "Action Required",
                Accent: "#FBBF24",
                SoftBackground: "#2A2210",
                Border: "#6B5415"),

            EmailTone.Danger => new EmailTheme(
                Label: "Important",
                Accent: "#FB7185",
                SoftBackground: "#2A151B",
                Border: "#6A2837"),

            _ => new EmailTheme(
                Label: "Otrade Update",
                Accent: "#38BDF8",
                SoftBackground: "#102431",
                Border: "#18506A")
        };
    }

    private enum EmailTone
    {
        Info,
        Success,
        Warning,
        Danger
    }

    private sealed record EmailTheme(
        string Label,
        string Accent,
        string SoftBackground,
        string Border);
}
