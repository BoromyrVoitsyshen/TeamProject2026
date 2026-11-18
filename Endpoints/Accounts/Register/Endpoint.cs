using AiChat.Api.Accounts;
using AiChat.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Accounts.Register;

public record RegisterRequest(string? Name, string? Email, string? Password, string? Phone, string? Role);

/// <summary>
/// Реєстрація клієнта або майстра. Після реєстрації користувач одразу входить.
/// Майстрові створюється порожній неопублікований профіль із розкладом Пн–Пт 09:00–18:00 —
/// він доповнює його в кабінеті й публікує.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", HandleAsync).RequireRateLimiting(AuthLimits.Policy)
            .AllowAnonymous(); // публічне: доступне гостям (див. FallbackPolicy в Accounts/AuthSetup.cs)
    }

    private static async Task<IResult> HandleAsync(RegisterRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var name = AccountRules.NormalizeText(request.Name);
        if (name.Length is < 2 or > AccountRules.MaxNameLength)
            return ApiResults.Error(StatusCodes.Status400BadRequest, $"Ім'я — від 2 до {AccountRules.MaxNameLength} символів.");

        var email = AccountRules.NormalizeEmail(request.Email);
        if (!AccountRules.IsValidEmail(email))
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Вкажіть правильний email.");

        var passwordError = AccountRules.PasswordError(request.Password);
        if (passwordError != null) return ApiResults.Error(StatusCodes.Status400BadRequest, passwordError);

        var (phone, phoneError) = AccountRules.NormalizePhone(request.Phone);
        if (phoneError != null) return ApiResults.Error(StatusCodes.Status400BadRequest, phoneError);

        UserRole role;
        switch (request.Role)
        {
            case "client": role = UserRole.Client; break;
            case "master": role = UserRole.Master; break;
            default: return ApiResults.Error(StatusCodes.Status400BadRequest, "Оберіть, хто ви: клієнт чи майстер.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return ApiResults.Error(StatusCodes.Status409Conflict, "Цей email уже зареєстровано. Увійдіть.");

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password!),
            Phone = phone,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Двоє зареєструвалися з тим самим email одночасно — спрацював унікальний індекс
            return ApiResults.Error(StatusCodes.Status409Conflict, "Цей email уже зареєстровано. Увійдіть.");
        }

        if (role == UserRole.Master)
        {
            var profile = new MasterProfile { UserId = user.Id, DisplayName = name, Phone = phone };
            db.Masters.Add(profile);
            await db.SaveChangesAsync(ct);

            // Типовий розклад, щоб майстру було з чого почати: Пн–Пт 09:00–18:00
            for (var weekday = 1; weekday <= 5; weekday++)
            {
                db.WorkingDays.Add(new WorkingDay
                {
                    MasterId = profile.Id,
                    Weekday = weekday,
                    Start = new TimeOnly(9, 0),
                    End = new TimeOnly(18, 0)
                });
            }

            await db.SaveChangesAsync(ct);
        }

        await CurrentUser.SignInAsync(http, user);
        return Results.Json(new { user = await CurrentUser.DescribeAsync(user, db, ct) }, statusCode: StatusCodes.Status201Created);
    }
}
