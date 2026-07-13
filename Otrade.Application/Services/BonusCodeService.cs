using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Bonus;
using Otrade.Application.DTOs.Common;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class BonusCodeService
{
    private readonly OtradeDbContext _context;
    private readonly INotificationQueue _notificationQueue;
    private readonly IEmailTemplateService _emailTemplateService;

    public BonusCodeService(
        OtradeDbContext context,
        INotificationQueue notificationQueue,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _notificationQueue = notificationQueue;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<ApiResponse<PagedResponse<BonusCodeDto>>> GetAdminBonusCodesAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? type = null,
        bool? isActive = null)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.BonusCodes
            .AsNoTracking()
            .Include(x => x.BonusRank)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Code.ToLower().Contains(text) ||
                (x.CampaignName != null && x.CampaignName.ToLower().Contains(text)) ||
                (x.Description != null && x.Description.ToLower().Contains(text)));
        }

        if (!string.IsNullOrWhiteSpace(type) &&
            !type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<BonusCodeType>(type, true, out var parsedType))
            {
                query = query.Where(x => x.BonusType == parsedType);
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BonusCodeDto
            {
                BonusCodeId = x.BonusCodeId,
                Code = x.Code,
                CampaignName = x.CampaignName,
                Description = x.Description,
                IsActive = x.IsActive,
                IsSingleUse = x.IsSingleUse,
                MaxUsageCount = x.MaxUsageCount,
                UsedCount = x.UsedCount,
                ExpiresAt = x.ExpiresAt,
                BonusType = x.BonusType.ToString(),
                BonusCapitalPercent = x.BonusCapitalPercent,
                BonusRankId = x.BonusRankId,
                BonusRankName = x.BonusRank != null ? x.BonusRank.Name : null,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return ResponseFactory.Success(new PagedResponse<BonusCodeDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        });
    }

    public async Task<ApiResponse<BonusCodeDto>> CreateAsync(
        CreateBonusCodeRequest request,
        long adminUserId)
    {
        if (request == null)
            return ResponseFactory.Fail<BonusCodeDto>("Invalid request");

        var code = NormalizeCode(request.Code);

        if (string.IsNullOrWhiteSpace(code))
            return ResponseFactory.Fail<BonusCodeDto>("Bonus code is required");

        if (code.Length > 50)
            return ResponseFactory.Fail<BonusCodeDto>("Bonus code cannot be longer than 50 characters");

        if (!Enum.TryParse<BonusCodeType>(request.BonusType, true, out var bonusType))
            return ResponseFactory.Fail<BonusCodeDto>("Invalid bonus type");

        var exists = await _context.BonusCodes
            .AnyAsync(x => x.Code == code);

        if (exists)
            return ResponseFactory.Fail<BonusCodeDto>("Bonus code already exists");

        if (request.MaxUsageCount <= 0)
            return ResponseFactory.Fail<BonusCodeDto>("Max usage count must be greater than zero");

        if (request.IsSingleUse && request.MaxUsageCount != 1)
            request.MaxUsageCount = 1;

        var validation = await ValidateBonusDefinitionAsync(
            bonusType,
            request.BonusCapitalPercent,
            request.BonusRankId);

        if (!validation.Success)
            return ResponseFactory.Fail<BonusCodeDto>(validation.Message);

        var entity = new BonusCode
        {
            Code = code,
            CampaignName = request.CampaignName?.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            IsSingleUse = request.IsSingleUse,
            MaxUsageCount = request.MaxUsageCount,
            UsedCount = 0,
            ExpiresAt = request.ExpiresAt,
            BonusType = bonusType,
            BonusCapitalPercent = request.BonusCapitalPercent,
            BonusRankId = request.BonusRankId,
            CreatedByAdminId = adminUserId,
            CreatedAt = DateTime.Now
        };

        _context.BonusCodes.Add(entity);

        await _context.SaveChangesAsync();

        var dto = await GetBonusCodeDtoAsync(entity.BonusCodeId);

        return ResponseFactory.Success(dto!, "Bonus code created successfully");
    }

    public async Task<ApiResponse<BonusCodeDto>> UpdateAsync(
        long bonusCodeId,
        UpdateBonusCodeRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<BonusCodeDto>("Invalid request");

        var entity = await _context.BonusCodes
            .FirstOrDefaultAsync(x => x.BonusCodeId == bonusCodeId);

        if (entity == null)
            return ResponseFactory.Fail<BonusCodeDto>("Bonus code not found");

        if (!Enum.TryParse<BonusCodeType>(request.BonusType, true, out var bonusType))
            return ResponseFactory.Fail<BonusCodeDto>("Invalid bonus type");

        if (request.MaxUsageCount <= 0)
            return ResponseFactory.Fail<BonusCodeDto>("Max usage count must be greater than zero");

        if (request.MaxUsageCount < entity.UsedCount)
            return ResponseFactory.Fail<BonusCodeDto>("Max usage count cannot be less than used count");

        if (request.IsSingleUse && request.MaxUsageCount != 1)
            request.MaxUsageCount = 1;

        var validation = await ValidateBonusDefinitionAsync(
            bonusType,
            request.BonusCapitalPercent,
            request.BonusRankId);

        if (!validation.Success)
            return ResponseFactory.Fail<BonusCodeDto>(validation.Message);

        entity.CampaignName = request.CampaignName?.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;
        entity.IsSingleUse = request.IsSingleUse;
        entity.MaxUsageCount = request.MaxUsageCount;
        entity.ExpiresAt = request.ExpiresAt;
        entity.BonusType = bonusType;
        entity.BonusCapitalPercent = request.BonusCapitalPercent;
        entity.BonusRankId = request.BonusRankId;
        entity.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        var dto = await GetBonusCodeDtoAsync(entity.BonusCodeId);

        return ResponseFactory.Success(dto!, "Bonus code updated successfully");
    }

    public async Task<ApiResponse<PagedResponse<BonusCodeUsageDto>>> GetAdminUsagesAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? status = null)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.BonusCodeUsages
            .AsNoTracking()
            .Include(x => x.BonusCode)
            .Include(x => x.User)
            .Include(x => x.AppliedRank)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.BonusCode!.Code.ToLower().Contains(text) ||
                x.User!.Email.ToLower().Contains(text) ||
                x.User.ReferralCode.ToLower().Contains(text) ||
                x.User.FirstName.ToLower().Contains(text) ||
                x.User.LastName.ToLower().Contains(text));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<BonusCodeUsageStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BonusCodeUsageDto
            {
                UsageId = x.UsageId,
                BonusCodeId = x.BonusCodeId,
                Code = x.BonusCode!.Code,
                CampaignName = x.BonusCode.CampaignName,
                UserId = x.UserId,
                UserEmail = x.User!.Email,
                UserUid = x.User.ReferralCode,
                UserFullName = (x.User.FirstName + " " + x.User.LastName).Trim(),
                RealCapitalAmount = x.RealCapitalAmount,
                BonusCapitalAmount = x.BonusCapitalAmount,
                AppliedRankId = x.AppliedRankId,
                AppliedRankName = x.AppliedRank != null ? x.AppliedRank.Name : null,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                CompletedAt = x.CompletedAt,
                AdminNote = x.AdminNote
            })
            .ToListAsync();

        return ResponseFactory.Success(new PagedResponse<BonusCodeUsageDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        });
    }

    public async Task<ApiResponse<MyBonusCodeUsageDto>> ApplyAsync(
        long userId,
        ApplyBonusCodeRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Invalid request");

        var code = NormalizeCode(request.Code);

        if (string.IsNullOrWhiteSpace(code))
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Bonus code is required");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var now = DateTime.Now;

        var bonusCode = await _context.BonusCodes
            .Include(x => x.BonusRank)
            .FirstOrDefaultAsync(x => x.Code == code);

        if (bonusCode == null)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Bonus code not found");

        if (!bonusCode.IsActive)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Bonus code is inactive");

        if (bonusCode.ExpiresAt.HasValue && bonusCode.ExpiresAt.Value < now)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Bonus code has expired");

        if (bonusCode.UsedCount >= bonusCode.MaxUsageCount)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Bonus code usage limit has been reached");

        var alreadyUsed = await _context.BonusCodeUsages
            .AnyAsync(x =>
                x.BonusCodeId == bonusCode.BonusCodeId &&
                x.UserId == userId);

        if (alreadyUsed)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("You have already used this bonus code");

        var investWallet = await _context.Wallets
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.WalletType == WalletType.Invest);

        if (investWallet == null)
            return ResponseFactory.Fail<MyBonusCodeUsageDto>("Invest wallet not found");

        var realCapitalAmount = investWallet.Balance;
        var bonusCapitalAmount = CalculateBonusCapital(
            bonusCode,
            realCapitalAmount);

        var usage = new BonusCodeUsage
        {
            BonusCodeId = bonusCode.BonusCodeId,
            UserId = userId,
            RealCapitalAmount = realCapitalAmount,
            BonusCapitalAmount = bonusCapitalAmount,
            AppliedRankId = bonusCode.BonusRankId,
            Status = BonusCodeUsageStatus.Active,
            CreatedAt = now,
            ExpiresAt = bonusCode.ExpiresAt,
            AdminNote = "Applied by user"
        };

        _context.BonusCodeUsages.Add(usage);

        bonusCode.UsedCount += 1;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user != null)
        {
            var emailBody = _emailTemplateService.GetBonusCodeAppliedEmail(
                bonusCode.Code,
                bonusCapitalAmount,
                bonusCode.BonusRank?.Name);

            await _notificationQueue.QueueEmailAsync(
                user.Email,
                "Bonus Code Applied",
                emailBody);
        }

        return ResponseFactory.Success(
            MapMyUsageDto(usage, bonusCode, bonusCode.BonusRank),
            "Bonus code applied successfully");
    }

    public async Task<ApiResponse<PagedResponse<MyBonusCodeUsageDto>>> GetMyUsagesAsync(
        long userId,
        int page = 1,
        int pageSize = 20)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.BonusCodeUsages
            .AsNoTracking()
            .Include(x => x.BonusCode)
            .Include(x => x.AppliedRank)
            .Where(x => x.UserId == userId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MyBonusCodeUsageDto
            {
                UsageId = x.UsageId,
                Code = x.BonusCode!.Code,
                CampaignName = x.BonusCode.CampaignName,
                RealCapitalAmount = x.RealCapitalAmount,
                BonusCapitalAmount = x.BonusCapitalAmount,
                AppliedRankName = x.AppliedRank != null ? x.AppliedRank.Name : null,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                CompletedAt = x.CompletedAt,
                AdminNote = x.AdminNote
            })
            .ToListAsync();

        return ResponseFactory.Success(new PagedResponse<MyBonusCodeUsageDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        });
    }
    public async Task<ApiResponse<BonusCodeUsageDto>> UpdateUsageStatusAsync(
        long usageId,
        UpdateBonusCodeUsageStatusRequest request)
    {
        if (request == null)
            return ResponseFactory.Fail<BonusCodeUsageDto>("Invalid request");

        if (!Enum.TryParse<BonusCodeUsageStatus>(request.Status, true, out var targetStatus))
            return ResponseFactory.Fail<BonusCodeUsageDto>("Invalid bonus usage status");

        if (targetStatus == BonusCodeUsageStatus.Active)
            return ResponseFactory.Fail<BonusCodeUsageDto>("Active status cannot be set manually");

        if (targetStatus is not BonusCodeUsageStatus.Cancelled
            and not BonusCodeUsageStatus.Expired)
        {
            return ResponseFactory.Fail<BonusCodeUsageDto>("Only Cancelled or Expired status can be set manually");
        }

        var usage = await _context.BonusCodeUsages
            .Include(x => x.BonusCode)
            .Include(x => x.User)
            .Include(x => x.AppliedRank)
            .FirstOrDefaultAsync(x => x.UsageId == usageId);

        if (usage == null)
            return ResponseFactory.Fail<BonusCodeUsageDto>("Bonus usage record not found");

        if (usage.Status != BonusCodeUsageStatus.Active)
            return ResponseFactory.Fail<BonusCodeUsageDto>("Only active bonus usage records can be updated");

        var adminNote = request.AdminNote?.Trim();

        if (!string.IsNullOrWhiteSpace(adminNote) && adminNote.Length > 1000)
            return ResponseFactory.Fail<BonusCodeUsageDto>("Admin note cannot be longer than 1000 characters");

        var now = DateTime.Now;

        usage.Status = targetStatus;
        usage.CompletedAt = now;

        if (targetStatus == BonusCodeUsageStatus.Expired &&
            (!usage.ExpiresAt.HasValue || usage.ExpiresAt.Value > now))
        {
            usage.ExpiresAt = now;
        }

        usage.AdminNote = !string.IsNullOrWhiteSpace(adminNote)
            ? adminNote
            : $"Changed manually by admin to {targetStatus}.";

        await _context.SaveChangesAsync();
        if (usage.User != null && !string.IsNullOrWhiteSpace(usage.User.Email))
        {
            var emailBody = _emailTemplateService.GetBonusUsageStatusChangedEmail(
                usage.BonusCode?.Code ?? "-",
                usage.Status.ToString(),
                usage.BonusCapitalAmount,
                usage.AppliedRank?.Name,
                usage.AdminNote);

            await _notificationQueue.QueueEmailAsync(
                usage.User.Email,
                $"Bonus Usage {usage.Status}",
                emailBody);
        }
        var dto = new BonusCodeUsageDto
        {
            UsageId = usage.UsageId,
            BonusCodeId = usage.BonusCodeId,
            Code = usage.BonusCode?.Code ?? string.Empty,
            CampaignName = usage.BonusCode?.CampaignName,
            UserId = usage.UserId,
            UserEmail = usage.User?.Email ?? string.Empty,
            UserUid = usage.User?.ReferralCode ?? string.Empty,
            UserFullName = $"{usage.User?.FirstName} {usage.User?.LastName}".Trim(),
            RealCapitalAmount = usage.RealCapitalAmount,
            BonusCapitalAmount = usage.BonusCapitalAmount,
            AppliedRankId = usage.AppliedRankId,
            AppliedRankName = usage.AppliedRank?.Name,
            Status = usage.Status.ToString(),
            CreatedAt = usage.CreatedAt,
            ExpiresAt = usage.ExpiresAt,
            CompletedAt = usage.CompletedAt,
            AdminNote = usage.AdminNote
        };

        return ResponseFactory.Success(
            dto,
            $"Bonus usage marked as {targetStatus} successfully");
    }
    private async Task<ApiResponse<bool>> ValidateBonusDefinitionAsync(
        BonusCodeType bonusType,
        decimal? bonusCapitalPercent,
        int? bonusRankId)
    {
        if (bonusType is BonusCodeType.CapitalBonus or BonusCodeType.Mixed)
        {
            if (!bonusCapitalPercent.HasValue || bonusCapitalPercent.Value <= 0)
                return ResponseFactory.Fail<bool>("Bonus capital percent must be greater than zero");

            if (bonusCapitalPercent.Value > 100)
                return ResponseFactory.Fail<bool>("Bonus capital percent cannot be greater than 100");
        }

        if (bonusType is BonusCodeType.RankUpgrade or BonusCodeType.Mixed)
        {
            if (!bonusRankId.HasValue)
                return ResponseFactory.Fail<bool>("Bonus rank is required");

            var rankExists = await _context.Ranks
                .AnyAsync(x => x.RankId == bonusRankId.Value);

            if (!rankExists)
                return ResponseFactory.Fail<bool>("Selected rank does not exist");
        }

        return ResponseFactory.Success(true);
    }

    private static decimal CalculateBonusCapital(
        BonusCode bonusCode,
        decimal realCapitalAmount)
    {
        if (bonusCode.BonusType is not BonusCodeType.CapitalBonus and not BonusCodeType.Mixed)
            return 0;

        var percent = bonusCode.BonusCapitalPercent ?? 0;

        return realCapitalAmount * percent / 100m;
    }

    private async Task<BonusCodeDto?> GetBonusCodeDtoAsync(long bonusCodeId)
    {
        return await _context.BonusCodes
            .AsNoTracking()
            .Include(x => x.BonusRank)
            .Where(x => x.BonusCodeId == bonusCodeId)
            .Select(x => new BonusCodeDto
            {
                BonusCodeId = x.BonusCodeId,
                Code = x.Code,
                CampaignName = x.CampaignName,
                Description = x.Description,
                IsActive = x.IsActive,
                IsSingleUse = x.IsSingleUse,
                MaxUsageCount = x.MaxUsageCount,
                UsedCount = x.UsedCount,
                ExpiresAt = x.ExpiresAt,
                BonusType = x.BonusType.ToString(),
                BonusCapitalPercent = x.BonusCapitalPercent,
                BonusRankId = x.BonusRankId,
                BonusRankName = x.BonusRank != null ? x.BonusRank.Name : null,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    private static MyBonusCodeUsageDto MapMyUsageDto(
        BonusCodeUsage usage,
        BonusCode bonusCode,
        Rank? appliedRank)
    {
        return new MyBonusCodeUsageDto
        {
            UsageId = usage.UsageId,
            Code = bonusCode.Code,
            CampaignName = bonusCode.CampaignName,
            RealCapitalAmount = usage.RealCapitalAmount,
            BonusCapitalAmount = usage.BonusCapitalAmount,
            AppliedRankName = appliedRank?.Name,
            Status = usage.Status.ToString(),
            CreatedAt = usage.CreatedAt,
            ExpiresAt = usage.ExpiresAt,
            CompletedAt = usage.CompletedAt,
            AdminNote = usage.AdminNote
        };
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
    }
}