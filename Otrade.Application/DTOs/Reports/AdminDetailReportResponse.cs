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
    public long DepositId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string UserFullName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string TxId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}

// جزئیات Withdrawal برای Admin
public class AdminWithdrawalDto
{
    public long WithdrawalId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string UserFullName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; } = string.Empty;

    public string Network { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}

// جزئیات WalletTransaction برای Admin
public class AdminWalletTransactionDto
{
    public long TransferId { get; set; }

    public string FromUserEmail { get; set; } = string.Empty;

    public string FromUserUid { get; set; } = string.Empty;

    public string ToUserEmail { get; set; } = string.Empty;

    public string ToUserUid { get; set; } = string.Empty;

    public string FromWalletType { get; set; } = string.Empty;

    public string ToWalletType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

// جزئیات Profit برای Admin
public class AdminProfitDto
{
    public string UserEmail { get; set; } = string.Empty;

    public string UserUid { get; set; } = string.Empty;

    public string? SourceUserEmail { get; set; }

    public string? SourceUserUid { get; set; }

    public string? SourceUserFullName { get; set; }

    public string ProfitType { get; set; } = string.Empty;

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