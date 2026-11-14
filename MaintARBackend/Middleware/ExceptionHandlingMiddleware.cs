using System.Text.Json;

namespace MaintARBackend.Middleware;

/// <summary>Ловит необработанные исключения и отдаёт клиенту аккуратный ответ 500 вместо подробностей ошибки.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var response = new { error = "Что-то пошло не так. Попробуйте позже." };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
