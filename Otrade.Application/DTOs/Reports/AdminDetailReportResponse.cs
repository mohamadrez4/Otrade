namespace Otrade.Application.DTOs.Reports;

// DTOهای مخصوص Admin
public class AdminDetailReportResponse
{
    public List<AdminDepositDto> Deposits { get; set; } = new();
    public List<AdminWithdrawalDto> Withdrawals { get; set; } = new();
    public List<AdminWalletTransactionDto> Transfers { get; set; } = new();
    public List<AdminProfitDto> InvestmentProfits { get; set; } = new();
    public List<AdminProfitDto> ReferralProfits { get; set; } = new();
    public List<AdminBonusDto> MainInvestBonuses { get; set; } = new();
}

// جزئیات Deposit برای Admin
public class AdminDepositDto
{
    public string UserEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

}

// جزئیات Withdrawal برای Admin
public class AdminWithdrawalDto
{
    public string UserEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// جزئیات WalletTransaction برای Admin
public class AdminWalletTransactionDto
{
    public string UserEmail { get; set; } = string.Empty;
    public string WalletType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// جزئیات Profit برای Admin
public class AdminProfitDto
{
    public string UserEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// جزئیات Main→Invest Bonus برای Admin
public class AdminBonusDto
{
    public string FromUserEmail { get; set; } = string.Empty;
    public string ToUserEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}