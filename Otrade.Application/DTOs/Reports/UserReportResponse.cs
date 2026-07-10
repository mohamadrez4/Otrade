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
    public long DepositId { get; set; }

    public decimal Amount { get; set; }

    public string TxId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}

public class WithdrawalDto
{
    public long WithdrawalId { get; set; }

    public decimal Amount { get; set; }

    public string WalletAddress { get; set; } = string.Empty;

    public string Network { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}

public class WalletTransactionDto
{
    public long TransferId { get; set; }

    public string Direction { get; set; } = string.Empty;

    public string FromWalletType { get; set; } = string.Empty;

    public string ToWalletType { get; set; } = string.Empty;

    public string FromUid { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string ToUid { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ProfitDto
{
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? SourceUserUid { get; set; }

    public string? SourceUserEmail { get; set; }

    public string? SourceUserFullName { get; set; }
}

public class BonusDto
{
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}