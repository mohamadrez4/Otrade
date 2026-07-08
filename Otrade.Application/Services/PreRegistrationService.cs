using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Auth;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System.ComponentModel.DataAnnotations;

namespace Otrade.Application.Services;

public class PreRegistrationService
{
    private readonly OtradeDbContext _context;
    private readonly SystemSettingService _settingService;

    public PreRegistrationService(
        OtradeDbContext context,
        SystemSettingService settingService)
    {
        _context = context;
        _settingService = settingService;
    }

    public async Task<ApiResponse<StartPreRegistrationResponse>> StartAsync(
        StartPreRegistrationRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Invalid request");

        var email = request.Email?.Trim().ToLowerInvariant();
        var referralCode = request.ReferralCode?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter email");

        if (!new EmailAddressAttribute().IsValid(email))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter a valid email address");

        if (string.IsNullOrWhiteSpace(referralCode))
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Please enter referral code");

        var emailExists = await _context.Users
            .AnyAsync(x => x.Email == email);

        if (emailExists)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Email already exists");

        var sponsor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReferralCode == referralCode);

        if (sponsor == null)
            return ResponseFactory.Fail<StartPreRegistrationResponse>("Referral code is invalid");

        var now = DateTime.Now;

        var activeTemporaryRegistration = await _context.TemporaryRegistrations
            .Where(x =>
                x.Email == email &&
                x.ExpiresAt > now &&
                x.Status != TemporaryRegistrationStatus.Completed &&
                x.Status != TemporaryRegistrationStatus.Rejected &&
                x.Status != TemporaryRegistrationStatus.Expired)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeTemporaryRegistration != null)
        {
            return ResponseFactory.Success(
                new StartPreRegistrationResponse
                {
                    TemporaryRegistrationId = activeTemporaryRegistration.Id,
                    Email = activeTemporaryRegistration.Email,
                    SponsorId = activeTemporaryRegistration.SponsorId,
                    Status = activeTemporaryRegistration.Status.ToString(),
                    ExpiresAt = activeTemporaryRegistration.ExpiresAt
                },
                "Pre-registration already exists");
        }

        var expireHours = await _settingService.GetIntAsync("InitialRegistrationExpireHours") ?? 72;

        var temporaryRegistration = new TemporaryRegistration
        {
            Email = email,
            ReferralCode = referralCode,
            SponsorId = sponsor.UserId,
            Status = TemporaryRegistrationStatus.EmailRegistered,
            ExpiresAt = now.AddHours(expireHours),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.TemporaryRegistrations.Add(temporaryRegistration);

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            new StartPreRegistrationResponse
            {
                TemporaryRegistrationId = temporaryRegistration.Id,
                Email = temporaryRegistration.Email,
                SponsorId = temporaryRegistration.SponsorId,
                Status = temporaryRegistration.Status.ToString(),
                ExpiresAt = temporaryRegistration.ExpiresAt
            },
            "Pre-registration started successfully");
    }
}