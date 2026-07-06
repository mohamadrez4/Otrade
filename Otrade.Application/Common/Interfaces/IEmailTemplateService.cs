namespace Otrade.Application.Common.Interfaces;

public interface IEmailTemplateService
{
    string GetVerificationEmail(string code);
    string GetDepositNotification(string userEmail, decimal amount, string txId);
    string GetWithdrawalNotification(string userEmail, decimal amount, string walletAddress);
    string GetTicketCreatedEmail(string userEmail, string subject);
    string GetTicketReplyEmail(string ticketSubject, string replyMessage);
    string GetKycApprovedEmail();
    string GetKycRejectedEmail(string reason);
    string GetWithdrawalApprovedEmail(decimal amount);
    string GetWithdrawalRejectedEmail(decimal amount, string reason);
    string GetPasswordResetEmail(string code);
}