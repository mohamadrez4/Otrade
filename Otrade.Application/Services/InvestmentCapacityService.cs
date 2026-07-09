using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using System.Globalization;
using Otrade.Application.DTOs.Wallet;
namespace Otrade.Application.Services;

public class InvestmentCapacityService
{
    private readonly OtradeDbContext _context;

    public InvestmentCapacityService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<InvestmentCapacityDto>>> GetAdminListAsync()
    {
        var items = await _context.InvestmentCapacities
            .AsNoTracking()
            .OrderByDescending(x => x.MonthStart)
            .Take(24)
            .Select(x => new InvestmentCapacityDto
            {
                CapacityId = x.CapacityId,
                Year = x.MonthStart.Year,
                Month = x.MonthStart.Month,
                MonthLabel = x.MonthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
                MonthStart = x.MonthStart,
                TotalCapacity = x.TotalCapacity,
                UsedCapacity = x.UsedCapacity,
                RemainingCapacity = x.TotalCapacity - x.UsedCapacity,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return ResponseFactory.Success(items);
    }

    public async Task<ApiResponse<InvestmentCapacityDto>> SaveAsync(
        SaveInvestmentCapacityRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<InvestmentCapacityDto>("Invalid request");

        if (request.Year < 2020 || request.Year > 2100)
            return ResponseFactory.Fail<InvestmentCapacityDto>("Invalid year");

        if (request.Month < 1 || request.Month > 12)
            return ResponseFactory.Fail<InvestmentCapacityDto>("Invalid month");

        if (request.TotalCapacity <= 0)
            return ResponseFactory.Fail<InvestmentCapacityDto>("Total capacity must be greater than zero");

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var now = DateTime.Now;

        var capacity = await _context.InvestmentCapacities
            .FirstOrDefaultAsync(x => x.MonthStart == monthStart);

        if (capacity == null)
        {
            capacity = new InvestmentCapacity
            {
                MonthStart = monthStart,
                TotalCapacity = request.TotalCapacity,
                UsedCapacity = 0,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.InvestmentCapacities.Add(capacity);
        }
        else
        {
            if (request.TotalCapacity < capacity.UsedCapacity)
            {
                return ResponseFactory.Fail<InvestmentCapacityDto>(
                    $"Total capacity cannot be less than used capacity. Used capacity: {capacity.UsedCapacity}");
            }

            capacity.TotalCapacity = request.TotalCapacity;
            capacity.IsActive = request.IsActive;
            capacity.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            new InvestmentCapacityDto
            {
                CapacityId = capacity.CapacityId,
                Year = capacity.MonthStart.Year,
                Month = capacity.MonthStart.Month,
                MonthLabel = capacity.MonthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
                MonthStart = capacity.MonthStart,
                TotalCapacity = capacity.TotalCapacity,
                UsedCapacity = capacity.UsedCapacity,
                RemainingCapacity = capacity.TotalCapacity - capacity.UsedCapacity,
                IsActive = capacity.IsActive,
                CreatedAt = capacity.CreatedAt,
                UpdatedAt = capacity.UpdatedAt
            },
            "Investment capacity saved");
    }

    public async Task<ApiResponse<bool>> ReserveCurrentMonthCapacityAsync(decimal amount)
    {
        if (amount <= 0)
            return ResponseFactory.Fail<bool>("Invalid investment amount");

        var now = DateTime.Now;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1);

        var capacity = await _context.InvestmentCapacities
            .FirstOrDefaultAsync(x =>
                x.MonthStart == monthStart &&
                x.IsActive);

        if (capacity == null)
            return ResponseFactory.Fail<bool>("Investment capacity for current month is not configured");

        var remaining = capacity.TotalCapacity - capacity.UsedCapacity;

        if (remaining <= 0)
            return ResponseFactory.Fail<bool>("Investment capacity for current month is full");

        if (amount > remaining)
            return ResponseFactory.Fail<bool>($"Investment capacity is not enough. Remaining capacity: {remaining.ToString("F2")}");

        capacity.UsedCapacity += amount;
        capacity.UpdatedAt = now;

        return ResponseFactory.Success(true, "Investment capacity reserved");
    }
    public async Task<ApiResponse<CurrentInvestmentCapacityResponse>> GetCurrentMonthCapacityAsync()
    {
        var now = DateTime.Now;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1);

        var capacity = await _context.InvestmentCapacities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonthStart == monthStart);

        if (capacity == null)
        {
            return ResponseFactory.Success(new CurrentInvestmentCapacityResponse
            {
                IsConfigured = false,
                IsActive = false,
                Year = monthStart.Year,
                Month = monthStart.Month,
                MonthLabel = monthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
                RemainingCapacity = 0
            });
        }

        var remaining = capacity.TotalCapacity - capacity.UsedCapacity;

        if (remaining < 0)
            remaining = 0;

        return ResponseFactory.Success(new CurrentInvestmentCapacityResponse
        {
            IsConfigured = true,
            IsActive = capacity.IsActive,
            Year = capacity.MonthStart.Year,
            Month = capacity.MonthStart.Month,
            MonthLabel = capacity.MonthStart.ToString("yyyy MMMM", CultureInfo.InvariantCulture),
            RemainingCapacity = remaining
        });
    }
}