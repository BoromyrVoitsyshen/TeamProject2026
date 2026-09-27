using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.RenameChat;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/chats/{id}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(string id, RenameRequest request, ChatStorage storage)
    {
        var chat = await storage.GetChatAsync(id);
        if (chat == null) return Results.NotFound(new { error = "Розмову не знайдено" });

        chat.Title = request.Title;
        chat.UpdatedAt = DateTime.UtcNow;

        await storage.SaveChatAsync(chat);

        return Results.Ok(new
        {
            id = chat.Id,
            title = chat.Title,
            updatedAt = chat.UpdatedAt
        });
    }
}

public class RenameRequest
{
    public string Title { get; set; } = string.Empty;
}