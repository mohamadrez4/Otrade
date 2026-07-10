namespace Otrade.Application.DTOs.Wallet
{
    public class WithdrawalPending
    {
        public long WithdrawalId { get; set; }

        public string UserEmail { get; set; } = string.Empty;

        public string UserUid { get; set; } = string.Empty;

        public string UserFullName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string WalletAddress { get; set; } = string.Empty;

        public string Network { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}