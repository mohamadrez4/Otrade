using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using System.Data;

namespace Otrade.Application.Services;

public class ContractService
{
    private readonly OtradeDbContext _context;

    public ContractService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<bool>> CreateContractAsync(long userId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var exists = await _context.Contracts
            .AnyAsync(x =>
                x.UserId == userId &&
                (
                    x.Status == ContractStatus.Active ||
                    x.Status == ContractStatus.PendingActivation
                ));

        if (exists)
        {
            return ResponseFactory.Fail<bool>(
                "You already have an active or pending investment agreement.");
        }

        var now = DateTime.Now;

        var contract = new Contract
        {
            UserId = userId,

            // زمان پذیرش قرارداد
            AcceptedAt = now,

            // این تاریخ‌ها فقط پس از اولین انتقال موفق
            // به Investing Wallet مقدار می‌گیرند.
            StartDate = null,
            EndDate = null,

            Status = ContractStatus.PendingActivation
        };

        _context.Contracts.Add(contract);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ResponseFactory.Success(
            true,
            "Agreement signed successfully. The six-month investment period will begin after your first successful transfer to the Investing Wallet.");
    }

    public async Task<ApiResponse<object>> GetCurrentContractAsync(long userId)
    {
        var now = DateTime.Now;

        /*
         * اول قرارداد Active را برمی‌گردانیم.
         * اگر Active وجود نداشت، آخرین PendingActivation نمایش داده می‌شود.
         */
        var contract = await _context.Contracts
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                (
                    x.Status == ContractStatus.Active ||
                    x.Status == ContractStatus.PendingActivation
                ))
            .OrderByDescending(x =>
                x.Status == ContractStatus.Active)
            .ThenByDescending(x => x.ContractId)
            .Select(x => new
            {
                x.ContractId,
                x.AcceptedAt,
                x.StartDate,
                x.EndDate,

                Status = x.Status.ToString(),

                IsActive =
                    x.Status == ContractStatus.Active,

                IsPendingActivation =
                    x.Status == ContractStatus.PendingActivation,

                RemainingDays =
                    x.EndDate.HasValue
                        ? EF.Functions.DateDiffDay(
                            now,
                            x.EndDate.Value)
                        : (int?)null
            })
            .FirstOrDefaultAsync();

        if (contract == null)
        {
            return ResponseFactory.Success<object>(new
            {
                HasContract = false,
                HasActiveContract = false,
                HasPendingActivation = false,
                Contract = (object?)null
            });
        }

        return ResponseFactory.Success<object>(new
        {
            HasContract = true,

            // برای سازگاری با Frontend فعلی نگه داشته شده است.
            HasActiveContract = contract.IsActive,

            HasPendingActivation =
                contract.IsPendingActivation,

            Contract = contract
        });
    }
}