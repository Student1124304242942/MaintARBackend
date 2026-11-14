using System.ComponentModel.DataAnnotations;

namespace MaintARBackend.Models.Auth;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Введите почту")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public required string Email { get; set; }
}
