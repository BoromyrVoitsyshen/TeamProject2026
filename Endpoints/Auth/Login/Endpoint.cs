using AiChat.Api.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.Auth.Login;

public class Endpoint : IEndpoint
{
    // Одне повідомлення для «немає такого email» і «неправильний пароль»: інакше за відповіддю
    // можна було б перевіряти, чи зареєстрована певна адреса
    private const string InvalidCredentials = "Неправильний email або пароль.";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", HandleAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest? request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn)
    {
        var email = request?.Email?.Trim() ?? string.Empty;
        var password = request?.Password ?? string.Empty;
        if (email.Length == 0 || password.Length == 0)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Вкажіть email і пароль.");

        var user = await users.FindByEmailAsync(email);
        if (user == null) return ApiResults.Error(StatusCodes.Status401Unauthorized, InvalidCredentials);

        // lockoutOnFailure: true — кожна невдала спроба рахується, після 5 вхід блокується на 5 хвилин
        var result = await signIn.PasswordSignInAsync(user, password, request!.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return ApiResults.Error(StatusCodes.Status429TooManyRequests,
                "Забагато невдалих спроб. Спробуйте ще раз за кілька хвилин.");

        if (!result.Succeeded) return ApiResults.Error(StatusCodes.Status401Unauthorized, InvalidCredentials);

        return Results.Ok(AuthUserResponse.From(user));
    }
}
