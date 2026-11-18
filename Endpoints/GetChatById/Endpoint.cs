using System.Security.Claims;
using AiChat.Api.Accounts;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.GetChatById;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats/{id}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(string id, ClaimsPrincipal user, ChatStorage storage, ChatBusyTracker busy)
    {
        var chat = await storage.GetChatAsync(user.RequireUserId(), id);
        if (chat == null) return ApiResults.ChatNotFound();

        // busy = true → після перезавантаження сторінка чекає відповідь агента (опитує кожні 2 с)
        return Results.Ok(ApiResults.ChatDetails(chat, busy));
    }
}
