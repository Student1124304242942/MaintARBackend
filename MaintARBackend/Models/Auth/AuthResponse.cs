namespace MaintARBackend.Models.Auth;

/// <summary>Ответ на успешную регистрацию или вход.</summary>
public class AuthResponse
{
    public required string Token { get; set; }

    public required string UserName { get; set; }

    public required string Email { get; set; }

    public bool IsEmailVerified { get; set; }
}
