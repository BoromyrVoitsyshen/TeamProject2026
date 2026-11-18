using AiChat.Api.Accounts;
using AiChat.Api.Persistence;

namespace AiChat.Api.Endpoints.Accounts.Me;

/// <summary>Хто увійшов. Гостю — { "user": null } (200, а не 401: це не помилка, просто ніхто не увійшов).</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(http, db, ct);
        if (user == null)
        {
            // cookie від акаунта, якого вже немає (напр. базу видалили) — прибираємо його
            if (http.User.GetUserId() != null) await CurrentUser.SignOutAsync(http);
            return Results.Ok(new { user = (object?)null });
        }

        return Results.Ok(new { user = await CurrentUser.DescribeAsync(user, db, ct) });
    }
}
