using AiChat.Api.Domain;

namespace AiChat.Api.Endpoints.Auth;

public record RegisterRequest(string? Email, string? Password);

/// <param name="RememberMe">true — cookie переживе закриття браузера (до 14 днів), false — лише до закриття.</param>
public record LoginRequest(string? Email, string? Password, bool RememberMe);

/// <summary>Що сторінка знає про користувача. Без хешу пароля та інших службових полів IdentityUser.</summary>
public record AuthUserResponse(string Id, string Email)
{
    public static AuthUserResponse From(AppUser user) => new(user.Id, user.Email ?? string.Empty);
}
