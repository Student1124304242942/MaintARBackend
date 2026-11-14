using System.ComponentModel.DataAnnotations;

namespace MaintARBackend.Models.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Введите почту")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    [MaxLength(256)]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Введите имя")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Имя должно быть от 2 до 50 символов")]
    public required string UserName { get; set; }

    [Required(ErrorMessage = "Введите пароль")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен быть не короче 8 символов")]
    public required string Password { get; set; }
}
