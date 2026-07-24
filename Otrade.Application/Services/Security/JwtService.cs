using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Otrade.Application.Services.Security;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(
        IConfiguration config)
    {
        _config =
            config;
    }

    /*
     * Compatibility overload for flows that create a brand-new user.
     * Existing login flows should use the overload that receives
     * AuthTokenVersion and MustChangePassword.
     */
    public string GenerateToken(
        long userId,
        string email,
        bool isAdmin,
        bool isOwner)
    {
        return GenerateToken(
            userId,
            email,
            isAdmin,
            isOwner,
            tokenVersion: 1,
            mustChangePassword: false);
    }

    public string GenerateToken(
        long userId,
        string email,
        bool isAdmin,
        bool isOwner,
        int tokenVersion,
        bool mustChangePassword)
    {
        var jwtSettings =
            _config.GetSection(
                "JwtSettings");

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtSettings["Key"]!));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var claims =
            new List<Claim>
            {
                new(
                    "userId",
                    userId.ToString()),

                new(
                    ClaimTypes.Email,
                    email),

                new(
                    "isAdmin",
                    isAdmin.ToString()),

                new(
                    "isOwner",
                    isOwner.ToString()),

                new(
                    "tokenVersion",
                    Math.Max(
                        1,
                        tokenVersion)
                    .ToString()),

                new(
                    "mustChangePassword",
                    mustChangePassword
                        .ToString())
            };

        var expiresInMinutes =
            int.Parse(
                jwtSettings[
                    "ExpireMinutes"
                ]!);

        var token =
            new JwtSecurityToken(
                issuer:
                    jwtSettings["Issuer"],

                audience:
                    jwtSettings["Audience"],

                claims:
                    claims,

                notBefore:
                    DateTime.UtcNow,

                expires:
                    DateTime.UtcNow
                        .AddMinutes(
                            expiresInMinutes),

                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(
                token);
    }
}