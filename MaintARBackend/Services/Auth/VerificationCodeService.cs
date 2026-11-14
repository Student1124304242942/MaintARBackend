using System.Security.Cryptography;
using MaintARBackend.Context;
using MaintARBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace MaintARBackend.Services.Auth;

public interface IVerificationCodeService
{
    /// <summary>
    /// Создаёт новый код взамен старого. Возвращает null, если предыдущий код отправлен совсем недавно.
    /// </summary>
    Task<string?> GenerateAsync(User user, CodePurpose purpose);

    /// <summary>
    /// Проверяет код. При неверном коде сам сохраняет счётчик попыток.
    /// При верном помечает код на удаление, но не сохраняет: вызывающий код сохраняет его
    /// одним SaveChanges вместе со своим изменением (подтверждение почты, новый пароль).
    /// </summary>
    Task<bool> VerifyAsync(User user, CodePurpose purpose, string code);
}

internal sealed class VerificationCodeService : IVerificationCodeService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxFailedAttempts = 5;

    private readonly AppDbContext _db;
    private readonly IPasswordService _passwordService;

    public VerificationCodeService(AppDbContext db, IPasswordService passwordService)
    {
        _db = db;
        _passwordService = passwordService;
    }

    public async Task<string?> GenerateAsync(User user, CodePurpose purpose)
    {
        var now = DateTime.UtcNow;

        var entry = await _db.VerificationCodes
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Purpose == purpose);

        if (entry != null && now - entry.SentAt < ResendCooldown)
        {
            return null;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var hash = _passwordService.Hash(user, code);

        if (entry == null)
        {
            entry = new VerificationCode { UserId = user.Id, Purpose = purpose, CodeHash = hash };
            _db.VerificationCodes.Add(entry);
        }

        entry.CodeHash = hash;
        entry.ExpiresAt = now.Add(CodeLifetime);
        entry.SentAt = now;
        entry.FailedAttempts = 0;

        await _db.SaveChangesAsync();

        return code;
    }

    public async Task<bool> VerifyAsync(User user, CodePurpose purpose, string code)
    {
        var entry = await _db.VerificationCodes
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Purpose == purpose);

        if (entry == null)
        {
            return false;
        }

        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _db.VerificationCodes.Remove(entry);
            await _db.SaveChangesAsync();

            return false;
        }

        // Защита от перебора: после лимита код сгорает, нужно запросить новый.
        // Строку не удаляем, чтобы пауза между письмами продолжала действовать:
        // иначе можно было бы запрашивать новый код сразу и перебирать без ограничений.
        if (entry.FailedAttempts >= MaxFailedAttempts)
        {
            return false;
        }

        if (!_passwordService.Verify(user, entry.CodeHash, code))
        {
            entry.FailedAttempts++;
            await _db.SaveChangesAsync();

            return false;
        }

        _db.VerificationCodes.Remove(entry);

        return true;
    }
}
