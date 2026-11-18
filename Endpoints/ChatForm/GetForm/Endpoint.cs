using System.Security.Claims;
using AiChat.Api.Accounts;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.ChatForm.GetForm;

/// <summary>Підсумок консультації розмови (MECH-86). 204 — ще не заповнювали.</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats/{id}/form", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(string id, ClaimsPrincipal user, ChatStorage storage)
    {
        var chat = await storage.GetChatAsync(user.RequireUserId(), id);
        if (chat == null) return ApiResults.ChatNotFound();

        return chat.Form == null ? Results.NoContent() : Results.Ok(chat.Form);
    }
}
