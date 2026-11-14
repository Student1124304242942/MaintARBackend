using MaintARBackend.Models;

namespace MaintARBackend.Services.Email;

/// <summary>
/// Заглушка для разработки: вместо отправки письма пишет код в консоль сервера.
/// Подключается только в режиме Development и только когда не задан Resend:ApiToken.
/// </summary>
internal sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendCodeAsync(string to, CodePurpose purpose, string code)
    {
        _logger.LogWarning("Письмо не отправлено (Resend не настроен). {Purpose} для {Email}: {Code}", purpose, to, code);

        return Task.CompletedTask;
    }
}
