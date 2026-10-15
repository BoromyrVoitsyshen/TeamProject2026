using AiChat.Api.Domain;
using AiChat.Api.Infrastructure;

namespace AiChat.Api.Endpoints;

/// <summary>Спільні відповіді API. Помилки завжди у форматі { "error": "текст" } — його показує сторінка.</summary>
public static class ApiResults
{
    public static IResult Error(int statusCode, string message) =>
        Results.Json(new { error = message }, statusCode: statusCode);

    public static IResult ChatNotFound() => Error(StatusCodes.Status404NotFound, "Розмову не знайдено");

    /// <summary>Повна розмова для GET /api/chats/{id} і POST /api/chats.</summary>
    public static object ChatDetails(ChatSession chat, ChatBusyTracker busy) => new
    {
        id = chat.Id,
        title = chat.Title,
        messages = chat.Messages,
        createdAt = chat.CreatedAt,
        updatedAt = chat.UpdatedAt,
        busy = busy.IsBusy(chat.Id)
    };
}
