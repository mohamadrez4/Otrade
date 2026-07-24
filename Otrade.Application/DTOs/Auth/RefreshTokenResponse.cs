namespace Otrade.Application.DTOs.Auth;

public class RefreshTokenResponse
{
    public string Token { get; set; }
        = string.Empty;

    public long UserId { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsOwner { get; set; }

    public bool MustChangePassword { get; set; }
}