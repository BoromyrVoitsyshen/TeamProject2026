using System.Security.Claims;
using AiChat.Api.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.Auth.Me;

/// <summary>Хто зараз увійшов. Сторінка питає це при завантаженні; 401 — показати вхід.</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        // Cookie може пережити акаунт (наприклад, користувача видалили з бази) — тоді теж 401
        var user = await users.GetUserAsync(principal);
        return user == null
            ? ApiResults.Error(StatusCodes.Status401Unauthorized, "Потрібно увійти.")
            : Results.Ok(AuthUserResponse.From(user));
    }
}
