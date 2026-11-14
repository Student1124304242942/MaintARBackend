using System.Security.Claims;
using MaintARBackend.Models.Auth;
using MaintARBackend.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintARBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);

        if (response == null)
        {
            return Conflict(new { error = "Пользователь с такой почтой уже существует" });
        }

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        if (response == null)
        {
            return Unauthorized(new { error = "Неверная почта или пароль" });
        }

        return Ok(response);
    }

    /// <summary>Данные текущего пользователя по токену.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { user.Id, user.UserName, user.Email, user.IsEmailVerified });
    }

    /// <summary>Отправляет код подтверждения на почту текущего пользователя.</summary>
    [Authorize]
    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.SendVerificationCodeAsync(userId);

        return result switch
        {
            SendCodeResult.Sent => Ok(new { message = "Код отправлен на почту" }),
            SendCodeResult.AlreadyVerified => BadRequest(new { error = "Почта уже подтверждена" }),
            SendCodeResult.TooManyRequests => StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { error = "Код уже отправлен. Повторно запросить можно через минуту" }),
            _ => Unauthorized()
        };
    }

    [Authorize]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (!await _authService.VerifyEmailAsync(userId, request.Code))
        {
            return BadRequest(new { error = "Неверный или просроченный код" });
        }

        return Ok(new { message = "Почта подтверждена" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);

        // Ответ одинаковый для любой почты, чтобы нельзя было узнать, зарегистрирована ли она.
        return Ok(new { message = "Если такая почта зарегистрирована, на неё отправлен код" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!await _authService.ResetPasswordAsync(request))
        {
            return BadRequest(new { error = "Неверный или просроченный код" });
        }

        return Ok(new { message = "Пароль изменён" });
    }
}
