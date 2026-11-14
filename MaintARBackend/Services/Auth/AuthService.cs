using MaintARBackend.Context;
using MaintARBackend.Models;
using MaintARBackend.Models.Auth;
using MaintARBackend.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace MaintARBackend.Services.Auth;

public enum SendCodeResult
{
    Sent,
    AlreadyVerified,
    TooManyRequests,
    UserNotFound
}

public interface IAuthService
{
    /// <summary>Создаёт пользователя. Возвращает null, если почта уже занята.</summary>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);

    /// <summary>Проверяет почту и пароль. Возвращает null, если они неверны.</summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request);

    Task<User?> GetUserAsync(int userId);

    /// <summary>Отправляет на почту пользователя код подтверждения.</summary>
    Task<SendCodeResult> SendVerificationCodeAsync(int userId);

    /// <summary>Подтверждает почту по коду. Возвращает false, если код неверный или истёк.</summary>
    Task<bool> VerifyEmailAsync(int userId, string code);

    /// <summary>Отправляет код для сброса пароля, если такая почта зарегистрирована.</summary>
    Task ForgotPasswordAsync(string email);

    /// <summary>Меняет пароль по коду из письма. Возвращает false, если код неверный или истёк.</summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
}

internal sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwordService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IVerificationCodeService _codeService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IPasswordService passwordService,
        IJwtTokenService jwtTokenService,
        IVerificationCodeService codeService,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
        _codeService = codeService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return null;
        }

        var user = new User
        {
            Email = email,
            UserName = request.UserName.Trim(),
            PasswordHash = string.Empty
        };
        user.PasswordHash = _passwordService.Hash(user, request.Password);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Два запроса с одной почтой пришли одновременно: первый успел, второй упёрся в уникальный индекс.
            if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email))
            {
                return null;
            }

            throw;
        }

        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !_passwordService.Verify(user, user.PasswordHash, request.Password))
        {
            return null;
        }

        return ToResponse(user);
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<SendCodeResult> SendVerificationCodeAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return SendCodeResult.UserNotFound;
        }

        if (user.IsEmailVerified)
        {
            return SendCodeResult.AlreadyVerified;
        }

        var code = await _codeService.GenerateAsync(user, CodePurpose.EmailVerification);

        if (code == null)
        {
            return SendCodeResult.TooManyRequests;
        }

        await _emailService.SendCodeAsync(user.Email, CodePurpose.EmailVerification, code);

        return SendCodeResult.Sent;
    }

    public async Task<bool> VerifyEmailAsync(int userId, string code)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !await _codeService.VerifyAsync(user, CodePurpose.EmailVerification, code))
        {
            return false;
        }

        user.IsEmailVerified = true;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == NormalizeEmail(email));

        if (user == null)
        {
            return;
        }

        var code = await _codeService.GenerateAsync(user, CodePurpose.PasswordReset);

        if (code == null)
        {
            return;
        }

        try
        {
            await _emailService.SendCodeAsync(user.Email, CodePurpose.PasswordReset, code);
        }
        catch (Exception ex)
        {
            // Ошибку отправки не показываем наружу: иначе по ответу можно понять, что почта зарегистрирована.
            _logger.LogError(ex, "Не удалось отправить код сброса пароля");
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == NormalizeEmail(request.Email));

        if (user == null || !await _codeService.VerifyAsync(user, CodePurpose.PasswordReset, request.Code))
        {
            return false;
        }

        user.PasswordHash = _passwordService.Hash(user, request.NewPassword);

        // Код пришёл на эту почту, значит пользователь ей владеет.
        user.IsEmailVerified = true;

        await _db.SaveChangesAsync();

        return true;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private AuthResponse ToResponse(User user)
    {
        return new AuthResponse
        {
            Token = _jwtTokenService.GenerateToken(user),
            UserName = user.UserName,
            Email = user.Email,
            IsEmailVerified = user.IsEmailVerified
        };
    }
}
