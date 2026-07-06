using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Otrade.Application.DTOs.Wallet
{
    public class WithdrawalPending
    {
        public long WithdrawalId { get; set; }

        public string UserEmail { get; set; }

        public decimal Amount { get; set; }

        public string WalletAddress { get; set; }

        public string Network { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
