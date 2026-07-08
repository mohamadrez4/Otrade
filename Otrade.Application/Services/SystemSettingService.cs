using Microsoft.EntityFrameworkCore;
using Otrade.Persistence.Context;
using System.Collections.Concurrent;

namespace Otrade.Application.Services;

public class SystemSettingService
{
    private readonly OtradeDbContext _context;

    private static readonly ConcurrentDictionary<string, string?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public SystemSettingService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = key.Trim();

        if (Cache.TryGetValue(key, out var cachedValue))
            return cachedValue;

        var value = await _context.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        Cache[key] = value;

        return value;
    }

    public async Task<string> GetRequiredValueAsync(string key)
    {
        var value = await GetValueAsync(key);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"System setting '{key}' is not configured.");

        return value;
    }

    public async Task<int?> GetIntAsync(string key)
    {
        var value = await GetValueAsync(key);

        if (int.TryParse(value, out var result))
            return result;

        return null;
    }

    public async Task<decimal?> GetDecimalAsync(string key)
    {
        var value = await GetValueAsync(key);

        if (decimal.TryParse(value, out var result))
            return result;

        return null;
    }

    public async Task<bool?> GetBoolAsync(string key)
    {
        var value = await GetValueAsync(key);

        if (bool.TryParse(value, out var result))
            return result;

        if (value == "1") return true;
        if (value == "0") return false;

        return null;
    }

    public async Task<Dictionary<string, string>> GetEmailSettingAsync()
    {
        var keys = new[]
        {
            "SmtpHost",
            "SmtpPort",
            "Username",
            "Password",
            "FromName"
        };

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var value = await GetValueAsync(key);

            if (!string.IsNullOrWhiteSpace(value))
                result[key] = value;
        }

        return result;
    }

    public void ClearCache()
    {
        Cache.Clear();
    }

    public void RemoveFromCache(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        Cache.TryRemove(key.Trim(), out _);
    }
}