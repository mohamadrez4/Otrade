namespace Otrade.Application.DTOs.Wallet
{
    public class DepositsPending
    {
        public long DepositId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string UserUid { get; set; } = string.Empty;

        public string UserFullName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string TxId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}