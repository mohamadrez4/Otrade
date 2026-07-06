using Microsoft.Extensions.DependencyInjection;
using Otrade.Application.Common.Locks;
using Otrade.Application.Services;

namespace Otrade.BackgroundJobs.Jobs;

public class RankJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobLockService _lockService;
    public RankJob(IServiceScopeFactory scopeFactory, JobLockService lockService)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
    }

    public async Task ExecuteAsync()
    {
        var locked = await _lockService.TryAcquireLockAsync("RankJob");

        if (!locked)
            return;
        using var scope = _scopeFactory.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<RankService>();

        await service.EvaluateAllRanksAsync();
    }
}