using MaintARBackend.Models;
using Microsoft.AspNetCore.Identity;

namespace MaintARBackend.Services.Auth;

public interface IPasswordService
{
    /// <summary>Хеширует пароль или одноразовый код.</summary>
    string Hash(User user, string value);

    /// <summary>Сверяет значение с ранее сохранённым хешем.</summary>
    bool Verify(User user, string hash, string value);
}

internal sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string Hash(User user, string value)
    {
        return _passwordHasher.HashPassword(user, value);
    }

    public bool Verify(User user, string hash, string value)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, hash, value);

        return result != PasswordVerificationResult.Failed;
    }
}
