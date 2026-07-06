using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Otrade.Application.DTOs.Wallet
{
    public class DepositsPending
    {
        public long depositId {  get; set; }
        public string Email { get; set; }

        public decimal Amount { get; set; }
        
        public string TxId { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
