namespace Otrade.Application.Common.Interfaces;

public interface IEmailTemplateService
{
    string GetVerificationEmail(string code);
    string GetDepositNotification(string userEmail, decimal amount, string txId);
    string GetDepositApprovedEmail(decimal requestedAmount, decimal approvedAmount);
    string GetDepositRejectedEmail(decimal? amount, string reason);
    string GetWithdrawalNotification(string userEmail, decimal amount, string walletAddress);
    string GetTicketCreatedEmail(string userEmail, string subject);
    string GetTicketReplyEmail(string ticketSubject, string replyMessage);
    string GetKycSubmittedAdminEmail(string userEmail,string userUid,string userFullName,string uploadedDocuments);
    string GetKycApprovedEmail();
    string GetKycRejectedEmail(string reason);
    string GetWithdrawalVerificationEmail(decimal amount,string walletAddress,string network,string code,int expiresInMinutes);
    string GetWithdrawalApprovedEmail(decimal amount);
    string GetWithdrawalRejectedEmail(decimal amount, string reason);
    string GetWithdrawalSubmittedEmail(decimal amount, string walletAddress);
    string GetWithdrawalCanceledEmail(decimal amount);
    string GetPasswordResetEmail(string code);
    string GetInternalTransferVerificationEmail(decimal amount,string receiverDisplay,string code,int expiresInMinutes);
    string GetInternalTransferCompletedEmail(decimal amount,string receiverDisplay);
    string GetInvestmentWaitListJoinedEmail(decimal requestedAmount);
    string GetInvestmentCapacityAvailableEmail(decimal requestedAmount);
    string GetInvestmentWaitListNotifiedEmail(decimal requestedAmount);
    string GetInvestWalletTransferEmail(decimal amount,string fromWalletType,decimal investWalletBalance);
}