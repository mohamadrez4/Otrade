using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.DTOs.Profile;
using Otrade.Persistence.Context;

namespace Otrade.Application.Services;

public class ProfileService
{
    private readonly OtradeDbContext _context;

    public ProfileService(
        OtradeDbContext context)
    {
        _context =
            context;
    }

    public async Task<ApiResponse<object>>GetProfileAsync(long userId)
    {
        var user =
            await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<object>(
                    "User not found");
        }

        var walletAddress =
            await _context.UserWalletAddresses
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        var kycDocuments =
            await _context.KycDocuments
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x => new
                {
                    x.DocumentId,
                    DocumentType =
                        x.DocumentType
                            .ToString(),
                    x.RejectReason,
                    Status =
                        x.Status.ToString(),
                    x.CreatedAt,
                    x.ReviewedAt
                })
                .ToListAsync();

        return ResponseFactory
            .Success<object>(
                new
                {
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.ReferralCode,

                    KycStatus =
                        user.KycStatus
                            .ToString(),

                    user.MustChangePassword,

                    user.TotpRecoveryLockedUntil,

                    WalletAddress =
                        walletAddress == null
                            ? null
                            : new
                            {
                                walletAddress.Address,
                                walletAddress.Network
                            },

                    KycDocuments =
                        kycDocuments
                });
    }

    public async Task<ApiResponse<bool>>ChangePasswordAsync(long userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.CurrentPassword))
        {
            return ResponseFactory
                .Fail<bool>(
                    "Current password is required");
        }

        if (string.IsNullOrWhiteSpace(
                request.NewPassword))
        {
            return ResponseFactory
                .Fail<bool>(
                    "New password is required");
        }

        if (request.NewPassword.Length < 6)
        {
            return ResponseFactory
                .Fail<bool>(
                    "New password must be at least 6 characters");
        }

        var user =
            await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (user == null)
        {
            return ResponseFactory
                .Fail<bool>(
                    "User not found");
        }

        var isValid =
            BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword,
                user.PasswordHash);

        if (!isValid)
        {
            return ResponseFactory
                .Fail<bool>(
                    "Current password is incorrect");
        }

        if (
            BCrypt.Net.BCrypt.Verify(
                request.NewPassword,
                user.PasswordHash)
        )
        {
            return ResponseFactory
                .Fail<bool>(
                    "New password must be different from the current password");
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                request.NewPassword);

        /*
         * This flag is set by approved 2FA recovery.
         * Once the password is changed, normal account access is restored.
         */
        user.MustChangePassword =
            false;

        user.UpdatedAt =
            DateTime.Now;

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            true,
            "Password changed successfully");
    }
}