using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common.Locks;
using Otrade.Application.Services;

namespace Otrade.BackgroundJobs.Jobs;

public class WalletBalanceSnapshotJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobLockservice _lockService;

    public WalletBalanceSnapshotJob(
        IServiceScopeFactory scopeFactory,
        JobLockservice lockService)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
    }

    public async Task ExecuteAsync()
    {
        var locked = await _lockService.TryAcquireLockAsync("WalletBalanceSnapshotJob");

        if (!locked)
            return;

        using var scope = _scopeFactory.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<WalletBalanceSnapshotService>();

        await service.CreateOrUpdateTodaySnapshotAsync();
    }
}