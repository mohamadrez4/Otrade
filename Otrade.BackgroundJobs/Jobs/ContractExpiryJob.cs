using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common.Locks;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.BackgroundJobs.Jobs;

public class ContractExpiryJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobLockservice _lockService;
    public ContractExpiryJob(IServiceScopeFactory scopeFactory, JobLockservice lockService)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
    }

    public async Task ExecuteAsync()
    {
        var locked = await _lockService.TryAcquireLockAsync("ContractExpiryJob");

        if (!locked)
            return;
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OtradeDbContext>();

        var expiredContracts = await context.Contracts
            .Where(x =>
                x.Status == ContractStatus.Active &&
                x.EndDate <=  DateTime.Now)
            .ToListAsync();

        foreach (var contract in expiredContracts)
        {
            contract.Status = ContractStatus.Completed;

            var investWallet = await context.Wallets.FirstOrDefaultAsync(x =>
                x.UserId == contract.UserId &&
                x.WalletType == WalletType.Invest);

            if (investWallet != null)
            {
                investWallet.IsLocked = false;
            }
        }

        await context.SaveChangesAsync();
    }
}