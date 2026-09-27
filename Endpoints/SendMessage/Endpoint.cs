using AiChat.Api.Domain;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.SendMessage;

public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats/{chatId}/messages", HandleAsync).DisableAntiforgery();
    }

    private static async Task<IResult> HandleAsync(
        string chatId,
        [FromForm(Name = "id")] string messageId,
        [FromForm(Name = "text")] string? text,
        IFormFileCollection images,
        ChatStorage storage)
    {
        var chat = await storage.GetChatAsync(chatId);
        if (chat == null) return Results.NotFound(new { error = "Розмову не знайдено" });

        var imageUrls = await storage.SaveImagesAsync(images);

        var userMessage = new ChatMessage
        {
            Id = messageId,
            Role = "user",
            Text = text ?? string.Empty,
            Images = imageUrls,
            CreatedAt = DateTime.UtcNow
        };
        chat.Messages.Add(userMessage);

        // TODO: Тут у майбутньому ми будемо викликати справжній ШІ
        // Поки що робимо заглушку-асистента
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "assistant",
            Text = $"Привіт! Ти написав: '{text}'. Я поки що заглушка, ШІ ще не підключено.",
            CreatedAt = DateTime.UtcNow
        };
        chat.Messages.Add(assistantMessage);

        chat.UpdatedAt = DateTime.UtcNow;
        await storage.SaveChatAsync(chat);

        return Results.Ok(new
        {
            userMessage,
            assistantMessage,
            chat = new
            {
                id = chat.Id,
                title = chat.Title,
                updatedAt = chat.UpdatedAt
            }
        });
    }
}