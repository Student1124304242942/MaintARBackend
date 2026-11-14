using MaintARBackend.Models;

namespace MaintARBackend.Services.Email;

public interface IEmailService
{
    /// <summary>Отправляет письмо с одноразовым кодом.</summary>
    Task SendCodeAsync(string to, CodePurpose purpose, string code);
}
