using MaintARBackend.Models;
using Resend;

namespace MaintARBackend.Services.Email;

/// <summary>Отправка писем через сервис Resend.</summary>
internal sealed class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _from;

    public ResendEmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _from = configuration["Resend:From"] ?? "onboarding@resend.dev";
    }

    public async Task SendCodeAsync(string to, CodePurpose purpose, string code)
    {
        var message = new EmailMessage
        {
            From = _from,
            Subject = EmailTemplates.Subject(purpose),
            TextBody = EmailTemplates.TextBody(purpose, code),
            HtmlBody = EmailTemplates.HtmlBody(purpose, code)
        };
        message.To.Add(to);

        await _resend.EmailSendAsync(message);
    }
}
