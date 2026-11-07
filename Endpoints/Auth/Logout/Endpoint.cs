using AiChat.Api.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.Auth.Logout;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // AllowAnonymous: вийти можна завжди, навіть якщо cookie вже прострочена — результат той самий
        app.MapPost("/api/auth/logout", HandleAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(SignInManager<AppUser> signIn)
    {
        // Видаляє cookie в браузері. Сама cookie на сервері ніде не зберігається, тому щоб вийти
        // «з усіх пристроїв», знадобиться UserManager.UpdateSecurityStampAsync
        await signIn.SignOutAsync();
        return Results.NoContent();
    }
}
