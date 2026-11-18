using System.Security.Claims;
using AiChat.Api.Persistence;
using AiChat.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Accounts;

/// <summary>
/// Хто зараз на сайті. У cookie лежить лише id користувача; роль та ім'я щоразу беремо з бази,
/// тож видалений акаунт чи змінена роль діють одразу.
/// </summary>
public static class CurrentUser
{
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Id для ендпоінтів, які вимагають входу (чати AI-помічника, фото): їх і так не відкрити без cookie
    /// (FallbackPolicy в AuthSetup), тож відсутній id — помилка налаштувань, а не запиту.
    /// </summary>
    public static int RequireUserId(this ClaimsPrincipal principal) =>
        principal.GetUserId() ?? throw new InvalidOperationException("Authenticated user has no numeric NameIdentifier claim.");

    /// <summary>Користувач із бази або null, якщо гість (чи cookie від акаунта, якого вже немає).</summary>
    public static async Task<User?> LoadAsync(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var id = http.User.GetUserId();
        if (id == null) return null;
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    /// <summary>Користувач із потрібною роллю, або готова відповідь 401/403 з поясненням для сторінки.</summary>
    public static async Task<(User? User, IResult? Error)> RequireAsync(
        HttpContext http, AppDbContext db, UserRole role, CancellationToken ct)
    {
        var user = await LoadAsync(http, db, ct);
        if (user == null)
            return (null, ApiResults.Error(StatusCodes.Status401Unauthorized, "Увійдіть в акаунт."));

        if (user.Role != role)
        {
            var message = role == UserRole.Master
                ? "Це доступно лише майстрам."
                : "Це доступно лише клієнтам. Увійдіть як клієнт.";
            return (null, ApiResults.Error(StatusCodes.Status403Forbidden, message));
        }

        return (user, null);
    }

    public static Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    public static Task SignOutAsync(HttpContext http) =>
        http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    /// <summary>Те, що сторінки знають про користувача: ім'я в шапці, роль, id профілю майстра.</summary>
    public static async Task<object> DescribeAsync(User user, AppDbContext db, CancellationToken ct)
    {
        int? masterId = null;
        if (user.Role == UserRole.Master)
        {
            masterId = await db.Masters.AsNoTracking()
                .Where(m => m.UserId == user.Id)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(ct);
        }

        return new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            phone = user.Phone,
            role = user.Role == UserRole.Master ? "master" : "client",
            masterId
        };
    }

    /// <summary>Ім'я для відгуків і записів: лише перше слово, щоб не показувати прізвище всім.</summary>
    public static string PublicName(string fullName)
    {
        var first = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Клієнт" : first;
    }
}
