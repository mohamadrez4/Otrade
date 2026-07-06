using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Kyc;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class KycService
{
    private readonly OtradeDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public KycService(
        OtradeDbContext context,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
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

        var existingDocs = await _context.KycDocuments
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var nationalDoc = existingDocs.FirstOrDefault(x => x.DocumentType == KycDocumentType.NationalId);
        var selfieDoc = existingDocs.FirstOrDefault(x => x.DocumentType == KycDocumentType.Selfie);

        var hasUploaded = false;

        // 🔹 National ID
        if (request.NationalIdImage != null)
        {
            var path = await _fileStorageService.SaveAsync(
                request.NationalIdImage,
                $"uploads/kyc/{userId}");

            if (nationalDoc != null)
            {
                // اگر مدرک رد شده بود یا موجود است → فقط بروزرسانی مسیر و وضعیت
                nationalDoc.FilePath = path;
                nationalDoc.Status = KycStatus.Pending;
            }
            else
            {
                _context.KycDocuments.Add(new KycDocument
                {
                    UserId = userId,
                    DocumentType = KycDocumentType.NationalId,
                    FilePath = path,
                    Status = KycStatus.Pending,
                    CreatedAt =  DateTime.Now
                });
            }

            hasUploaded = true;
        }

        // 🔹 Selfie
        if (request.SelfieImage != null)
        {
            var path = await _fileStorageService.SaveAsync(
                request.SelfieImage,
                $"uploads/kyc/{userId}");

            if (selfieDoc != null)
            {
                selfieDoc.FilePath = path;
                selfieDoc.Status = KycStatus.Pending;
            }
            else
            {
                _context.KycDocuments.Add(new KycDocument
                {
                    UserId = userId,
                    DocumentType = KycDocumentType.Selfie,
                    FilePath = path,
                    Status = KycStatus.Pending,
                    CreatedAt =  DateTime.Now
                });
            }

            hasUploaded = true;
        }

        if (!hasUploaded)
            return ResponseFactory.Fail<bool>("No document uploaded");

        // وضعیت کلی KYC
        user.KycStatus = KycStatus.Pending;
        user.UpdatedAt =  DateTime.Now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "KYC submitted successfully");
    }
}