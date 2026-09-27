using AiChat.Api.Domain;
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

    private static async Task<IResult> HandleAsync(ChatStorage storage)
    {
        var newChat = new ChatSession();
        await storage.SaveChatAsync(newChat);
        return Results.Ok(newChat);
    }
}