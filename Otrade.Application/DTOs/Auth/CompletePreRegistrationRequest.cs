namespace Otrade.Application.DTOs.Auth;

public class CompletePreRegistrationRequest
{
    public string CompletionToken { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}