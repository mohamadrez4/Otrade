using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Otrade.Application.Common;
using Otrade.Application.Common.Interfaces;
using Otrade.Application.DTOs.Auth;
using Otrade.Application.Services.Security;
using Otrade.Domain.Entities;
using Otrade.Domain.Enums;
using Otrade.Persistence.Context;
using System.Security.Cryptography;
using System.Text;
public class AuthService
{
    private readonly OtradeDbContext _context;
    private readonly JwtService _jwtService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationQueue _notificationQueue;

    public AuthService(
        OtradeDbContext context,
        JwtService jwtService,
        IEmailTemplateService emailTemplateService,
        INotificationQueue notificationQueue)
    {
        _context = context;
        _jwtService = jwtService;
        _emailTemplateService = emailTemplateService;
        _notificationQueue = notificationQueue;
    }
    // REGISTER
    public async Task<ApiResponse<bool>> RegisterAsync(RegisterRequest request)
    {
        if (request.FirstName == null || request.FirstName == string.Empty)
            return ResponseFactory.Fail<bool>("Please enter Firstname");
        if (request.LastName == null || request.LastName == string.Empty)
            return ResponseFactory.Fail<bool>("Please enter LastName");
        if (request.Email == null || request.Email == string.Empty)
            return ResponseFactory.Fail<bool>("Please enter LastName");
        if (request.Password == null || request.Password == string.Empty)
            return ResponseFactory.Fail<bool>("Please enter Password");
        if (request.ReferralCode == null || request.ReferralCode == string.Empty)
            return ResponseFactory.Fail<bool>("Please enter ReferralCode");
        // 1. check email
        if (await _context.Users.AnyAsync(x => x.Email == request.Email))
            return ResponseFactory.Fail<bool>("Email already exists");

        // 2. check referral
        var sponsor = await _context.Users
            .FirstOrDefaultAsync(x => x.ReferralCode == request.ReferralCode);

        if (sponsor == null)
            return ResponseFactory.Fail<bool>("Referral code is invalid.");

        // 3. create user
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),

            ReferralCode = await GenerateCode(),

            SponsorId = sponsor.UserId,

            EmailConfirmed = false,
            IsOwner = false,
            IsAdmin = false,

            KycStatus = KycStatus.Pending,
            CurrentRankId = 1,
            CreatedAt =  DateTime.Now,
            UpdatedAt =  DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var codegen = GenerateVerificationCode();
        var code = new EmailVerificationCode
        {
            UserId = user.UserId,
            Code = codegen,
            IsUsed = false,
            CreatedAt =  DateTime.Now,
            ExpireAt =  DateTime.Now.AddMinutes(10)
        };

        _context.EmailVerificationCodes.Add(code);

        await _context.SaveChangesAsync();
        var verificationEmailBody = _emailTemplateService.GetVerificationEmail(codegen);

        await _notificationQueue.QueueEmailAsync(
            user.Email,
            "Otrade Email Verification",
            verificationEmailBody);
        return ResponseFactory.Success(true, "verification code send");
    }
    // LOGIN
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return ResponseFactory.Fail<LoginResponse>("Invalid credentials");

        var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isValid)
            return ResponseFactory.Fail<LoginResponse>("Invalid credentials");

        if (!user.EmailConfirmed)
            return ResponseFactory.Fail<LoginResponse>("Email not verified");
        var token = _jwtService.GenerateToken(
                   user.UserId,
                   user.Email,
                   user.IsAdmin,
                   user.IsOwner);

        var response = new LoginResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            ReferralCode = user.ReferralCode,
            IsAdmin = user.IsAdmin,
            IsOwner = user.IsOwner,
            EmailConfirmed = user.EmailConfirmed,
            KycStatus = user.KycStatus.ToString(),
            Token = token
        };

        return ResponseFactory.Success(response,"Login successful.");
    }

    public async Task<ApiResponse<bool>> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
        {
            return ResponseFactory.Fail<bool>(
                "User not found.");
        }

        if (user.EmailConfirmed)
        {
            return ResponseFactory.Fail<bool>(
                "Email already verified.");
        }

        var verification = await _context.EmailVerificationCodes
            .Where(x => x.UserId == user.UserId)
            .OrderByDescending(x => x.VerificationId)
            .FirstOrDefaultAsync();

        if (verification == null)
        {
            return ResponseFactory.Fail<bool>(
                "Verification code not found.");
        }

        if (verification.IsUsed)
        {
            return ResponseFactory.Fail<bool>(
                "Verification code already used.");
        }

        if (verification.ExpireAt <  DateTime.Now)
        {
            return ResponseFactory.Fail<bool>(
                "Verification code expired.");
        }

        if (verification.Code != request.Code)
        {
            return ResponseFactory.Fail<bool>(
                "Invalid verification code.");
        }

        verification.IsUsed = true;
        user.CurrentRankId = 1;
        user.EmailConfirmed = true;
        var wallets = new List<Wallet>
            {
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Main,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Invest,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Profit,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Referral,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                }
            };

        _context.Wallets.AddRange(wallets);

        await AddReferralRelationsAsync(user.UserId, user.SponsorId);

        await _context.SaveChangesAsync();

        return ResponseFactory.Success(
            true,
            "Email verified successfully.");
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ResendVerificationRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return ResponseFactory.Fail<bool>("Email not found");

        // تولید کد 6 رقمی OTP).
        var otp = GenerateVerificationCode();

        // ذخیره در جدول EmailVerificationCodes یا یک جدول جدید PasswordReset
        _context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = user.UserId,
            Code = otp,
            IsUsed = false,
            CreatedAt =  DateTime.Now,
            ExpireAt =  DateTime.Now.AddMinutes(15) // 15 دقیقه اعتبار
        });

        await _context.SaveChangesAsync();

        var passwordResetEmailBody = _emailTemplateService.GetPasswordResetEmail(otp);

        await _notificationQueue.QueueEmailAsync(
            user.Email,
            "Otrade Password Reset",
            passwordResetEmailBody);

        return ResponseFactory.Success(true, "Password reset code sent to your email");
    }
    public async Task<ApiResponse<bool>> ChangePasswordAsync( ChangePassword request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return ResponseFactory.Fail<bool>("User not found");

        var verification = await _context.EmailVerificationCodes
            .Where(x => x.UserId == user.UserId && x.Code == request.Code && !x.IsUsed && x.ExpireAt >  DateTime.Now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (verification == null)
            return ResponseFactory.Fail<bool>("Invalid or expired code");

        // تغییر رمز
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Mark code used
        verification.IsUsed = true;
        user.EmailConfirmed = true;

        var walllet = await _context.Wallets
            .FirstOrDefaultAsync(x => x.UserId == user.UserId);
        if (walllet == null)
        {
            var wallets = new List<Wallet>
            {
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Main,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Invest,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Profit,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                },
                new Wallet
                {
                    UserId = user.UserId,
                    WalletType = WalletType.Referral,
                    Balance = 0,
                    IsLocked = false,
                    CreatedAt =  DateTime.Now
                }
            };

            _context.Wallets.AddRange(wallets);
            await AddReferralRelationsAsync(user.UserId, user.SponsorId);
        }
        await _context.SaveChangesAsync();

        return ResponseFactory.Success(true, "Password changed successfully");
    }

    private async Task AddReferralRelationsAsync(long newUserId, long? sponsorId)
    {
        if (sponsorId == null)
            return;

        var exists = await _context.ReferralRelations
            .AnyAsync(x =>
                x.AncestorId == sponsorId.Value &&
                x.DescendantId == newUserId);

        if (!exists)
        {
            _context.ReferralRelations.Add(new ReferralRelation
            {
                AncestorId = sponsorId.Value,
                DescendantId = newUserId,
                Depth = 1
            });
        }

        var ancestors = await _context.ReferralRelations
            .Where(x => x.DescendantId == sponsorId.Value)
            .Select(x => new
            {
                x.AncestorId,
                x.Depth
            })
            .ToListAsync();

        foreach (var ancestor in ancestors)
        {
            var relationExists = await _context.ReferralRelations
                .AnyAsync(x =>
                    x.AncestorId == ancestor.AncestorId &&
                    x.DescendantId == newUserId);

            if (relationExists)
                continue;

            _context.ReferralRelations.Add(new ReferralRelation
            {
                AncestorId = ancestor.AncestorId,
                DescendantId = newUserId,
                Depth = ancestor.Depth + 1
            });
        }
    }
    private async Task<string> GenerateCode()
    {
        var gencode = "OTR" + GenerateVerificationCode();
        var user = await _context.Users.AnyAsync(x => x.ReferralCode == gencode);
        if (!user)
            return gencode;
        return await GenerateCode();
    }
    private string GenerateVerificationCode()
    {
        var code = RandomNumberGenerator
        .GetInt32(100000, 999999)
        .ToString();

        return code;
    }
}