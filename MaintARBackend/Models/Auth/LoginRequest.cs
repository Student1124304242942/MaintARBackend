using System.ComponentModel.DataAnnotations;

namespace MaintARBackend.Models.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Введите почту")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Введите пароль")]
    public required string Password { get; set; }
}
