using AiChat.Api.Domain;
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

    private static async Task<IResult> HandleAsync(string id, RenameRequest? request, ChatStorage storage)
    {
        if (!ChatStorage.IsValidId(id)) return ApiResults.ChatNotFound();

        // Ті самі правила, що на сторінці: пробіли зводимо до одного, порожньо — не можна, до 100 символів
        var title = ChatText.NormalizeSpaces(request?.Title);
        if (title.Length == 0)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Назва не може бути порожньою.");
        if (title.Length > ChatText.MaxTitleLength)
            return ApiResults.Error(StatusCodes.Status400BadRequest,
                $"Назва — до {ChatText.MaxTitleLength} символів.");

        // UpdateChatAsync, а не Get + Save: інакше перейменування могло б затерти повідомлення,
        // яке агент дописав у цю розмову між нашим читанням і записом
        var chat = await storage.UpdateChatAsync(id, c =>
        {
            c.Title = title;
            c.UpdatedAt = DateTime.UtcNow;
        });

        return chat == null ? ApiResults.ChatNotFound() : Results.Ok(ChatSummary.From(chat));
    }
}

public class RenameRequest
{
    public string? Title { get; set; }
}
