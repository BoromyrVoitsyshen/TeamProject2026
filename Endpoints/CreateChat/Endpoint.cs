using AiChat.Api.Domain;
using System.Security.Claims;
using AiChat.Api.Accounts;
using AiChat.Api.Infrastructure;
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
        await storage.SaveChatAsync(user.RequireUserId(), newChat);

        // 201 Created + Location — стандартна відповідь на створення ресурсу
        return Results.Created($"/api/chats/{newChat.Id}", ApiResults.ChatDetails(newChat, busy));
    }
}
