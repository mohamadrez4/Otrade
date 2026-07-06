using Microsoft.EntityFrameworkCore;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Common.Locks;

public class JobLockService
{
    private readonly OtradeDbContext _context;

    public JobLockService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAcquireLockAsync(string jobName, int lockMinutes = 10)
    {
        var lockEntry = await _context.Set<JobLock>()
            .FirstOrDefaultAsync(x => x.JobName == jobName);

        if (lockEntry != null)
        {
            // اگر هنوز lock معتبر است
            if (lockEntry.LockedAt >  DateTime.Now.AddMinutes(-lockMinutes))
                return false;

            lockEntry.LockedAt =  DateTime.Now;
        }
        else
        {
            _context.Add(new JobLock
            {
                JobName = jobName,
                LockedAt =  DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }
}