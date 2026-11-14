using System.ComponentModel.DataAnnotations;

namespace MaintARBackend.Models.Auth;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Введите почту")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Введите код")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Код состоит из 6 цифр")]
    public required string Code { get; set; }

    [Required(ErrorMessage = "Введите новый пароль")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен быть не короче 8 символов")]
    public required string NewPassword { get; set; }
}
