using AiChat.Api.Accounts;

namespace AiChat.Api.Endpoints.Accounts.Logout;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", HandleAsync)
            .AllowAnonymous(); // публічне: доступне гостям (див. FallbackPolicy в Accounts/AuthSetup.cs)
    }

    // Другий параметр потрібен: метод лише з HttpContext ASP.NET Core вважає RequestDelegate
    // і відкидає повернений IResult — відповідь була б порожньою
    private static async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        await CurrentUser.SignOutAsync(http);
        return Results.Ok(new { user = (object?)null });
    }
}
