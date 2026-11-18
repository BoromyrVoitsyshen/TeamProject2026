using AiChat.Api.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.Auth.Register;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", HandleAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest? request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn)
    {
        var email = request?.Email?.Trim() ?? string.Empty;
        var password = request?.Password ?? string.Empty;
        if (email.Length == 0 || password.Length == 0)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Вкажіть email і пароль.");

        // Email і є логіном: окремого імені користувача в нас немає
        var user = new AppUser { UserName = email, Email = email };

        // CreateAsync сам перевіряє формат email, унікальність і правила пароля (див. AddAppAuth),
        // і зберігає не пароль, а його хеш (PBKDF2 із сіллю)
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var duplicate = result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName");
            var message = string.Join(" ", result.Errors.Select(e => e.Description).Distinct());
            return ApiResults.Error(duplicate ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest, message);
        }

        // Одразу входимо, щоб після реєстрації не довелося вводити пароль ще раз
        await signIn.SignInAsync(user, isPersistent: true);
        return Results.Created("/api/auth/me", AuthUserResponse.From(user));
    }
}
