using AiChat.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace AiChat.Api.Infrastructure.Auth;

/// <summary>
/// Вхід через cookie на основі ASP.NET Core Identity.
/// Чому cookie, а не JWT: сайт і API на одному домені; HttpOnly-cookie недоступна JavaScript-у (XSS не
/// вкраде сесію); браузер сам додає її до fetch і до &lt;img&gt; (фото з /uploads теж під авторизацією).
/// Мобільний клієнт чи вхід через основний автосайт (OpenID Connect) додаються пізніше як ще одна схема.
/// </summary>
public static class AuthSetup
{
    public const string CookieName = "aichat.auth";

    public static IdentityBuilder AddAppAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        // AddIdentityCore, а не AddIdentity: без ролей і без сторінок Razor — нам потрібні лише
        // користувачі, паролі й вхід. Сторінки входу в нас свої (wwwroot/login.html)
        var identity = services.AddIdentityCore<AppUser>(options =>
            {
                // Сучасна рекомендація (NIST): довжина важливіша за «обов'язково велика літера і символ»
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.User.RequireUniqueEmail = true;

                // Захист від підбору пароля: 5 невдалих спроб — вхід заблоковано на 5 хвилин
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddSignInManager()
            .AddErrorDescriber<UkrainianIdentityErrorDescriber>();

        services.AddHttpContextAccessor();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            // Strict: браузер не надсилає cookie із запитами, які почалися на іншому сайті — це наш захист від CSRF.
            // Піддомени того самого сайту (наприклад, майбутній автосайт) для SameSite — «свої»
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Локально сайт працює і по http://localhost:5079, тому в Development дозволяємо http;
            // на сервері cookie йде лише по HTTPS
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // За замовчуванням cookie-схема перенаправляє на /Account/Login (302) — це для серверних сторінок.
            // Для API потрібні коди: 401 «увійдіть», 403 «немає доступу». Сторінка сама відкриє login.html
            options.Events.OnRedirectToLogin = context =>
                WriteError(context.Response, StatusCodes.Status401Unauthorized, "Потрібно увійти.");
            options.Events.OnRedirectToAccessDenied = context =>
                WriteError(context.Response, StatusCodes.Status403Forbidden, "Немає доступу.");
        });

        // Secure by default: будь-який ендпоінт вимагає входу, якщо явно не позначений AllowAnonymous.
        // Новий ендпоінт, про авторизацію якого забули, не стане публічним випадково.
        // На статичні файли (html, css, js) це не впливає — їх віддає UseStaticFiles ще до авторизації
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // Cookie зашифрована ключами Data Protection. За замовчуванням вони живуть у профілі користувача ОС,
        // а в Docker — усередині контейнера: після перезапуску контейнера всіх би «викинуло» з акаунтів.
        // Тримаємо ключі поруч із даними (data/keys), у тому ж volume
        services.AddDataProtection()
            .SetApplicationName("AiChat")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(DataPaths.GetDataDirectory(builder.Configuration, builder.Environment), "keys")));

        return identity;
    }

    private static Task WriteError(HttpResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(new { error = message });
    }
}
