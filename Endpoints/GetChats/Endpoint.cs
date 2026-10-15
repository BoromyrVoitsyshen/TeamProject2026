using AiChat.Api.Domain;
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

        // preview потрібен панелі зліва; без нього там скрізь «Без повідомлень»
        return Results.Ok(chats.Select(ChatSummary.From));
    }
}
