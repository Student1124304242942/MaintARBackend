using System.ComponentModel.DataAnnotations;

namespace MaintARBackend.Models.Auth;

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Введите код")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Код состоит из 6 цифр")]
    public required string Code { get; set; }
}
