using AiChat.Api.Domain;
using System.Security.Claims;
using AiChat.Api.Infrastructure;
using AiChat.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.CreateChat;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(ClaimsPrincipal user, ChatStorage storage, ChatBusyTracker busy)
    {
        var newChat = new ChatSession();
        await storage.SaveChatAsync(user.GetUserId(), newChat);

        // 201 Created + Location — стандартна відповідь на створення ресурсу
        return Results.Created($"/api/chats/{newChat.Id}", ApiResults.ChatDetails(newChat, busy));
    }
}
