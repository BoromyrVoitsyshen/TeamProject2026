using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AiChat.Api.Accounts;

/// <summary>Ліміт спроб входу й реєстрації: 10 на хвилину з однієї IP-адреси (захист від підбору пароля).</summary>
public static class AuthLimits
{
    public const string Policy = "auth";
    public const int PermitsPerMinute = 10;
}

public static class AuthSetup
{
    /// <summary>Вхід через cookie + ліміт спроб. Викликається з Program.cs.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "mechs.auth";
                options.Cookie.HttpOnly = true;               // JavaScript сторінки cookie не бачить
                options.Cookie.SameSite = SameSiteMode.Lax;   // чужі сайти не можуть надіслати запит від імені користувача
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                // Це API: замість переходу на сторінку входу — просто 401/403, сторінка сама покаже, що робити
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, ct) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Забагато спроб. Зачекайте хвилину і спробуйте ще раз." }, ct);
            };
            limiter.AddPolicy(AuthLimits.Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthLimits.PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        return services;
    }
}
