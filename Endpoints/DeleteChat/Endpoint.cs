using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.DeleteChat;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/chats/{id}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(string id, ChatStorage storage)
    {
        await storage.DeleteChatAsync(id);

        return Results.Ok();
    }
}