using System.Text;
using MaintARBackend.Context;
using MaintARBackend.Middleware;
using MaintARBackend.Services.Auth;
using MaintARBackend.Services.Email;
using Resend;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Ключ подписи токенов лежит в пользовательских секретах, а не в репозитории.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Не задан Jwt:Key (нужно не меньше 32 символов). Задайте его командой: dotnet user-secrets set \"Jwt:Key\" \"<ключ>\"");
}

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IVerificationCodeService, VerificationCodeService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Письма идут через Resend. Без токена в режиме разработки код пишется в консоль сервера.
var resendToken = builder.Configuration["Resend:ApiToken"];
if (!string.IsNullOrWhiteSpace(resendToken))
{
    builder.Services.AddOptions();
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.Configure<ResendClientOptions>(options =>
    {
        options.ApiToken = resendToken;

        // Если Resend отказал в отправке, это должно быть ошибкой, а не молчаливым успехом.
        options.ThrowExceptions = true;
    });
    builder.Services.AddTransient<IResend, ResendClient>();
    builder.Services.AddScoped<IEmailService, ResendEmailService>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();
}
else
{
    throw new InvalidOperationException(
        "Не задан Resend:ApiToken. Задайте его командой: dotnet user-secrets set \"Resend:ApiToken\" \"<токен>\"");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Вставь сюда только сам токен, без слова Bearer"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
