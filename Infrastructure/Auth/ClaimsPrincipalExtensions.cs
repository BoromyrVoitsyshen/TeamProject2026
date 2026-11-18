using System.Security.Claims;

namespace AiChat.Api.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id користувача з cookie (claim NameIdentifier ставить ASP.NET Identity при вході).
    /// Ендпоінти чатів вимагають входу (FallbackPolicy у Program.cs), тож без id сюди не дійти —
    /// виняток означає помилку в налаштуваннях авторизації, а не в запиті.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim.");
}
