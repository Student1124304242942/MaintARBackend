using MaintARBackend.Models;

namespace MaintARBackend.Services.Email;

public static class EmailTemplates
{
    public static string Subject(CodePurpose purpose)
    {
        return purpose switch
        {
            CodePurpose.EmailVerification => "Код подтверждения почты",
            CodePurpose.PasswordReset => "Код для сброса пароля",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
    }

    public static string TextBody(CodePurpose purpose, string code)
    {
        return $"{Subject(purpose)}: {code}. Код действует 15 минут. Если это были не вы — просто проигнорируйте это письмо.";
    }

    public static string HtmlBody(CodePurpose purpose, string code)
    {
        return $@"
<div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background-color: #f4f4f7;"">
    <div style=""background: #ffffff; border-radius: 8px; padding: 32px; text-align: center;"">
        <h2 style=""color: #1a1a1a; margin: 0 0 8px;"">MaintAR</h2>
        <p style=""color: #555555; font-size: 15px; margin: 0 0 24px;"">{Subject(purpose)}</p>
        <div style=""margin: 0 0 24px; padding: 16px; border: 1px dashed #cccccc; border-radius: 6px; font-size: 28px; letter-spacing: 6px; font-weight: bold; color: #2d2d2d; background: #fafafa;"">
            {code}
        </div>
        <p style=""color: #555555; font-size: 13px; margin: 0;"">Код действует 15 минут.</p>
        <p style=""color: #999999; font-size: 12px; margin: 24px 0 0;"">Если это были не вы — просто проигнорируйте это письмо.</p>
    </div>
</div>";
    }
}
