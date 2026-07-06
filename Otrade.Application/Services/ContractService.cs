using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;
using System;

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
        var exists = await _context.Contracts
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Status == ContractStatus.Active);

        if (exists)
            return ResponseFactory.Fail<bool>("Contract already active");

        var contract = new Contract
        {
            UserId = userId,
            StartDate =  DateTime.Now,
            EndDate =  DateTime.Now.AddMonths(6),
            Status = ContractStatus.Active
        };

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();
        return ResponseFactory.Success(true, "Contract created");
    }

    public async Task<ApiResponse<object>> GetCurrentContractAsync(long userId)
    {
        var now =  DateTime.Now;

        var contract = await _context.Contracts
            .Where(x =>
                x.UserId == userId &&
                x.Status == ContractStatus.Active &&
                x.EndDate >= now)
            .OrderByDescending(x => x.ContractId)
            .Select(x => new
            {
                x.ContractId,
                x.StartDate,
                x.EndDate,
                Status = x.Status.ToString(),
                RemainingDays = EF.Functions.DateDiffDay(now, x.EndDate)
            })
            .FirstOrDefaultAsync();

        if (contract == null)
        {
            return ResponseFactory.Success<object>(new
            {
                HasActiveContract = false,
                Contract = (object?)null
            });
        }

        return ResponseFactory.Success<object>(new
        {
            HasActiveContract = true,
            Contract = contract
        });
    }
}