
namespace Otrade.Application.DTOs.Auth
{
    public class ChangePassword
    {
        public string Email { get; set; }
        public string Code { get; set; }
        public string NewPassword { get; set; }
    }
}
