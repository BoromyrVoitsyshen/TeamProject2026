using AiChat.Api.Accounts;
using AiChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Accounts.Login;

public record LoginRequest(string? Email, string? Password);

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", HandleAsync).RequireRateLimiting(AuthLimits.Policy);
    }

    private static async Task<IResult> HandleAsync(LoginRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var email = AccountRules.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Перевіряємо пароль навіть для неіснуючого email (з «порожнім» хешем), щоб відповідь не підказувала,
        // чи є такий акаунт. І повідомлення одне на обидва випадки.
        var valid = PasswordHasher.Verify(request.Password ?? string.Empty, user?.PasswordHash);
        if (user == null || !valid)
            return ApiResults.Error(StatusCodes.Status401Unauthorized, "Невірний email або пароль.");

        await CurrentUser.SignInAsync(http, user);
        return Results.Ok(new { user = await CurrentUser.DescribeAsync(user, db, ct) });
    }
}
