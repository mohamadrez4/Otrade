using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Admin;
using Otrade.Domain.Entities;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class SystemSettingService
{
    private readonly OtradeDbContext _context;

    public SystemSettingService(OtradeDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        return await _context.SystemSettings
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
    }
    public async Task<Dictionary<string, string>> GetEmailSettingAsync()
    {
        var keys = new[] { "SmtpHost", "SmtpPort", "Username", "Password", "FromName" };
        var settings = await _context.SystemSettings
                 .Where(x => keys.Contains(x.Key))
                .ToDictionaryAsync(x => x.Key, x => x.Value);
        return settings;
    }
}