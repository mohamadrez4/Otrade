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
    public string GetWithdrawalVerificationEmail(decimal amount,string walletAddress,string network,string code,int expiresInMinutes)
    {
        return $@"
        <h2>Withdrawal Verification</h2>
        <p>You requested a withdrawal from your Main Wallet.</p>
        <p>Amount: {amount:F2} USDT</p>
        <p>Network: {network}</p>
        <p>Wallet Address: {walletAddress}</p>
        <p>Your verification code is:</p>
        <h1 style='letter-spacing:4px'>{code}</h1>
        <p>This code expires in {expiresInMinutes} minutes.</p>
        <p>If you did not request this withdrawal, please contact support immediately.</p>
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
    public string GetDepositApprovedEmail(decimal requestedAmount, decimal approvedAmount)
    {
        return $@"
        <h2>Deposit Approved</h2>
        <p>Your deposit request has been approved.</p>
        <p>Requested Amount: {requestedAmount:F2} USDT</p>
        <p>Approved Amount: {approvedAmount:F2} USDT</p>
        <p>The approved amount has been credited to your Main Wallet.</p>
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
    public string GetWithdrawalSubmittedEmail(decimal amount, string walletAddress)
    {
        return $@"
        <h2>Withdrawal Request Submitted</h2>
        <p>Your withdrawal request has been submitted successfully.</p>
        <p>Amount: {amount:F2} USDT</p>
        <p>Wallet Address: {walletAddress}</p>
        <p>Status: Pending admin review</p>
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
    public string GetWithdrawalCanceledEmail(decimal amount)
    {
        return $@"
        <h2>Withdrawal Canceled</h2>
        <p>Your withdrawal request has been canceled.</p>
        <p>Amount: {amount:F2} USDT</p>
        <p>The reserved amount has been returned to your Main Wallet.</p>
    ";
    }
    public string GetDepositRejectedEmail(decimal? amount, string reason)
    {
        return $@"
            <h2>Deposit Rejected</h2>
            <p>Your Deposit of {amount:F2} USDT has been rejected.</p>
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
    public string GetInternalTransferVerificationEmail(
    decimal amount,
    string receiverDisplay,
    string code,
    int expiresInMinutes)
    {
        return $@"
        <h2>Internal Transfer Verification</h2>
        <p>You requested an internal transfer from your Main Wallet.</p>
        <p>Receiver: {receiverDisplay}</p>
        <p>Amount: {amount:F2} USDT</p>
        <p>Your verification code is:</p>
        <h1 style='letter-spacing:4px'>{code}</h1>
        <p>This code expires in {expiresInMinutes} minutes.</p>
        <p>If you did not request this transfer, please ignore this email and contact support.</p>
    ";
    }

    public string GetInternalTransferCompletedEmail(
        decimal amount,
        string receiverDisplay)
    {
        return $@"
        <h2>Internal Transfer Completed</h2>
        <p>Your internal transfer has been completed successfully.</p>
        <p>Receiver: {receiverDisplay}</p>
        <p>Amount: {amount:F2} USDT</p>
    ";
    }
}