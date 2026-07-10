using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Kyc;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System.IO;

namespace Otrade.Application.Services;

public class KycService
{
    private readonly OtradeDbContext _context;
    private readonly INotificationQueue _notificationQueue;
    private readonly IFileStorageService _fileStorageService;
    private readonly SystemSettingService _systemSettingService;
    private readonly IEmailTemplateService _emailTemplateService;

    public KycService(
        OtradeDbContext context,
        INotificationQueue notificationQueue,
        IFileStorageService fileStorageService,
        SystemSettingService systemSettingService,
        IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _notificationQueue = notificationQueue;
        _fileStorageService = fileStorageService;
        _systemSettingService = systemSettingService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<ApiResponse<bool>> UploadAsync(
        KycUploadRequest request,
        long userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return ResponseFactory.Fail<bool>("User not found");

        if (user.KycStatus == KycStatus.Approved)
            return ResponseFactory.Fail<bool>("KYC already approved");

        if (request.NationalIdImage == null && request.SelfieImage == null)
            return ResponseFactory.Fail<bool>("No document uploaded");

        if (request.NationalIdImage != null)
        {
            var validation = await ValidateKycFileAsync(
                request.NationalIdImage,
                "National ID");

            if (!validation.Success)
                return validation;
        }

        if (request.SelfieImage != null)
        {
            var validation = await ValidateKycFileAsync(
                request.SelfieImage,
                "Selfie");

            if (!validation.Success)
                return validation;
        }

        var existingDocs = await _context.KycDocuments
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var nationalDoc = existingDocs.FirstOrDefault(x =>
            x.DocumentType == KycDocumentType.NationalId);

        var selfieDoc = existingDocs.FirstOrDefault(x =>
            x.DocumentType == KycDocumentType.Selfie);

        var now = DateTime.Now;

        var uploadedDocuments = new List<string>();

        if (request.NationalIdImage != null)
        {
            var path = await _fileStorageService.SaveAsync(
                request.NationalIdImage,
                $"uploads/kyc/{userId}");

            if (nationalDoc != null)
            {
                nationalDoc.FilePath = path;
                nationalDoc.Status = KycStatus.Pending;
                nationalDoc.RejectReason = null;
                nationalDoc.ReviewedAt = null;
                nationalDoc.ReviewedByAdminId = null;
                nationalDoc.CreatedAt = now;
            }
            else
            {
                _context.KycDocuments.Add(new KycDocument
                {
                    UserId = userId,
                    DocumentType = KycDocumentType.NationalId,
                    FilePath = path,
                    Status = KycStatus.Pending,
                    RejectReason = null,
                    ReviewedAt = null,
                    ReviewedByAdminId = null,
                    CreatedAt = now
                });
            }

            uploadedDocuments.Add("National ID");
        }

        if (request.SelfieImage != null)
        {
            var path = await _fileStorageService.SaveAsync(
                request.SelfieImage,
                $"uploads/kyc/{userId}");

            if (selfieDoc != null)
            {
                selfieDoc.FilePath = path;
                selfieDoc.Status = KycStatus.Pending;
                selfieDoc.RejectReason = null;
                selfieDoc.ReviewedAt = null;
                selfieDoc.ReviewedByAdminId = null;
                selfieDoc.CreatedAt = now;
            }
            else
            {
                _context.KycDocuments.Add(new KycDocument
                {
                    UserId = userId,
                    DocumentType = KycDocumentType.Selfie,
                    FilePath = path,
                    Status = KycStatus.Pending,
                    RejectReason = null,
                    ReviewedAt = null,
                    ReviewedByAdminId = null,
                    CreatedAt = now
                });
            }

            uploadedDocuments.Add("Selfie");
        }

        user.KycStatus = KycStatus.Pending;
        user.UpdatedAt = now;

        await _context.SaveChangesAsync();

        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "-";

        var adminBody = _emailTemplateService.GetKycSubmittedAdminEmail(
            user.Email,
            user.ReferralCode,
            fullName,
            string.Join(", ", uploadedDocuments));

        await _notificationQueue.QueueAdminAsync(
            "New KYC Submission",
            adminBody);

        return ResponseFactory.Success(
            true,
            "KYC submitted successfully");
    }

    private async Task<ApiResponse<bool>> ValidateKycFileAsync(
        IFormFile file,
        string title)
    {
        if (file == null || file.Length == 0)
            return ResponseFactory.Fail<bool>($"{title} file is required");

        var allowedExtensionsValue = await _systemSettingService
            .GetValueAsync("KycAllowedExtensions");

        if (string.IsNullOrWhiteSpace(allowedExtensionsValue))
            allowedExtensionsValue = ".jpg,.jpeg,.png,.pdf";

        var allowedExtensions = allowedExtensionsValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (!allowedExtensions.Any())
            allowedExtensions = new List<string>
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".pdf"
            };

        var extension = Path.GetExtension(file.FileName)
            ?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension) ||
            !allowedExtensions.Contains(extension))
        {
            return ResponseFactory.Fail<bool>(
                $"{title} file type is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
        }

        var maxSizeMb = await _systemSettingService
            .GetIntAsync("KycMaxFileSizeMb") ?? 5;

        if (maxSizeMb <= 0)
            maxSizeMb = 5;

        var maxSizeBytes = maxSizeMb * 1024L * 1024L;

        if (file.Length > maxSizeBytes)
        {
            return ResponseFactory.Fail<bool>(
                $"{title} file size is too large. Maximum allowed size is {maxSizeMb} MB");
        }

        return ResponseFactory.Success(true, "KYC submitted successfully");
    }
}