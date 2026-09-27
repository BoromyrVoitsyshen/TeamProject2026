using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.GetChats;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(ChatStorage storage)
    {
        var chats = await storage.GetAllChatsAsync();

        var summary = chats.Select(c => new
        {
            id = c.Id,
            title = c.Title,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt
        });

        return Results.Ok(summary);
    }
}