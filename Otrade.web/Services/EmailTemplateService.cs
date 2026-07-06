using Otrade.Application.Common.Interfaces;

namespace Otrade.Web.Services;

public class EmailTemplateService : IEmailTemplateService
{
    public string GetVerificationEmail(string code)
    {
        return $@"
            <h2>Welcome to Otrade</h2>
            <p>Your verification code is:</p>
            <h1>{code}</h1>
            <p>This code expires in 10 minutes.</p>
        ";
    }

    public string GetDepositNotification(string userEmail, decimal amount, string txId)
    {
        return $@"
            <h2>New Deposit Request</h2>
            <p>User: {userEmail}</p>
            <p>Amount: {amount:F2} USDT</p>
            <p>TXID: {txId}</p>
        ";
    }

    public string GetWithdrawalNotification(string userEmail, decimal amount, string walletAddress)
    {
        return $@"
            <h2>New Withdrawal Request</h2>
            <p>User: {userEmail}</p>
            <p>Amount: {amount:F2} USDT</p>
            <p>Wallet Address: {walletAddress}</p>
        ";
    }

    public string GetTicketCreatedEmail(string userEmail, string subject)
    {
        return $@"
            <h2>New Ticket Created</h2>
            <p>User: {userEmail}</p>
            <p>Subject: {subject}</p>
        ";
    }

    public string GetTicketReplyEmail(string ticketSubject, string replyMessage)
    {
        return $@"
            <h2>Your ticket has been replied</h2>
            <p>Subject: {ticketSubject}</p>
            <p>Reply: {replyMessage}</p>
        ";
    }

    public string GetKycApprovedEmail()
    {
        return $@"
            <h2>KYC Approved</h2>
            <p>Your KYC documents have been approved.</p>
        ";
    }

    public string GetKycRejectedEmail(string reason)
    {
        return $@"
            <h2>KYC Rejected</h2>
            <p>Your KYC documents were rejected.</p>
            <p>Reason: {reason}</p>
        ";
    }

    public string GetWithdrawalApprovedEmail(decimal amount)
    {
        return $@"
            <h2>Withdrawal Approved</h2>
            <p>Your withdrawal of {amount:F2} USDT has been approved.</p>
        ";
    }

    public string GetWithdrawalRejectedEmail(decimal amount, string reason)
    {
        return $@"
            <h2>Withdrawal Rejected</h2>
            <p>Your withdrawal of {amount:F2} USDT has been rejected.</p>
            <p>Reason: {reason}</p>
        ";
    }
    public string GetPasswordResetEmail(string code)
    {
        return $@"
        <div style='font-family:Arial;padding:20px'>
            <h2>Password Reset Request</h2>

            <p>You requested a password reset for your Otrade account.</p>

            <p>Your reset code is:</p>

            <div style='
                font-size:32px;
                font-weight:bold;
                letter-spacing:8px;
                margin:20px 0;
                color:#2563eb'>
                {code}
            </div>

            <p>
                This code will expire in
                <strong>15 minutes</strong>.
            </p>

            <p>
                If you did not request this reset,
                please ignore this email.
            </p>

            <hr/>

            <small>
                Otrade Security System
            </small>
        </div>";
    }
}