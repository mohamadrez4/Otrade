namespace Otrade.Application.DTOs.Reports;

public class UserReportResponse
{
    public List<DepositDto> Deposits { get; set; } = new();
    public List<WithdrawalDto> Withdrawals { get; set; } = new();
    public List<WalletTransactionDto> Transfers { get; set; } = new();
    public List<ProfitDto> InvestmentProfits { get; set; } = new();
    public List<ProfitDto> ReferralProfits { get; set; } = new();
    public List<BonusDto> MainInvestBonuses { get; set; } = new();
}

public class DepositDto
{
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WithdrawalDto
{
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WalletTransactionDto
{
    public string WalletType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ProfitDto
{
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BonusDto
{
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}