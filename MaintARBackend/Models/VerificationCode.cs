namespace MaintARBackend.Models;

/// <summary>
/// Одноразовый код из письма. У пользователя не больше одного действующего кода на каждое назначение.
/// </summary>
public class VerificationCode
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public CodePurpose Purpose { get; set; }

    /// <summary>Сам код в базе не хранится, только его хеш.</summary>
    public required string CodeHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Сколько раз ввели неверный код. После лимита код больше не принимается.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Когда код отправили: не даёт запрашивать письма слишком часто.</summary>
    public DateTime SentAt { get; set; }
}
