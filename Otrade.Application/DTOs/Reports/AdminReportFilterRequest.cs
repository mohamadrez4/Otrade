namespace Otrade.Application.DTOs.Reports;

public class AdminReportFilterRequest
{
    public string? Email { get; set; }

    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public decimal? MinAmount { get; set; }

    public decimal? MaxAmount { get; set; }
}