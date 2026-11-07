using System.Security.Claims;
using AiChat.Api.Infrastructure;
using AiChat.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.GetUpload;

/// <summary>
/// Віддає фото з розмов. Раніше /uploads був просто папкою зі статичними файлами — будь-хто, хто знав
/// адресу, бачив фото. Тепер це ендпоінт за авторизацією: файл шукається лише в папці того, хто увійшов,
/// тож чуже фото дасть 404, навіть якщо знати його адресу.
/// </summary>
public class Endpoint : IEndpoint
{
    // Віддаємо лише фото — навіть якщо на диску якось опиниться .html чи .svg, браузер його не отримає
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/uploads/{fileName}", Handle);
    }

    private static IResult Handle(string fileName, ClaimsPrincipal user, ChatStorage storage, HttpContext http)
    {
        if (!ContentTypes.TryGetValue(Path.GetExtension(fileName), out var contentType) ||
            !storage.TryGetUploadPath(user.GetUserId(), $"/uploads/{fileName}", out var path) ||
            !File.Exists(path))
        {
            return Results.NotFound();
        }

        // private — фото не повинні осідати в спільних кешах (проксі), лише в браузері цього користувача.
        // Ім'я файлу випадкове й не змінюється, тож добу можна не перепитувати сервер.
        http.Response.Headers.CacheControl = "private, max-age=86400";
        return Results.File(path, contentType, enableRangeProcessing: true);
    }
}
