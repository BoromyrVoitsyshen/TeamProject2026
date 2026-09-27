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

    private static async Task<IResult> HandleAsync(string id, ChatStorage storage)
    {
        var chat = await storage.GetChatAsync(id);

        if (chat == null)
        {
            return Results.NotFound(new { error = "Розмову не знайдено" });
        }

        var response = new
        {
            id = chat.Id,
            title = chat.Title,
            messages = chat.Messages,
            createdAt = chat.CreatedAt,
            busy = false
        };

        return Results.Ok(response);
    }
}