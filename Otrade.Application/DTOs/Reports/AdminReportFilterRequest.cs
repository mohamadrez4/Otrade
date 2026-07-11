namespace Otrade.Application.DTOs.Reports;

public class AdminReportFilterRequest
{
    public string? Type { get; set; }

    public string? Email { get; set; }

    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public decimal? MinAmount { get; set; }

    public decimal? MaxAmount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}