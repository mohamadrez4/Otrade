using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Otrade.BackgroundJobs.Jobs;

namespace Otrade.BackgroundJobs.Scheduler;

public static class JobScheduler
{
    public static void Register(IServiceProvider services)
    {
        var recurringJobManager =
            services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<RankJob>(
            "rank-job",
            job => job.ExecuteAsync(),
             "0 0 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")
            });

        recurringJobManager.AddOrUpdate<ContractExpiryJob>(
            "contract-expiry-job",
            job => job.ExecuteAsync(),
            Cron.Hourly,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")
            });

        recurringJobManager.AddOrUpdate<InvestmentProfitJob>(
            "investment-profit-job",
            job => job.ExecuteAsync(),
            "0 0 * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran") });
    }
}