namespace Otrade.Domain.Entities;

public class JobLock
{
    public string JobName { get; set; }

    public DateTime LockedAt { get; set; }
}