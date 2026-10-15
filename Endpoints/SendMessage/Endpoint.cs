using AiChat.Api.Agent;
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
        // TODO: прибрати DisableAntiforgery, коли з'явиться вхід через cookie
        app.MapPost("/api/chats/{chatId}/messages", HandleAsync).DisableAntiforgery();
    }

    private static async Task<IResult> HandleAsync(
        string chatId,
        [FromForm(Name = "id")] string? messageId,
        [FromForm(Name = "text")] string? text,
        IFormFileCollection images,
        ChatStorage storage,
        ChatBusyTracker busy,
        AgentReplyRunner runner,
        CancellationToken ct)
    {
        // 1. Перевіряємо все, що можна, ще до того, як щось записати на диск
        if (!ChatStorage.IsValidId(chatId)) return ApiResults.ChatNotFound();

        text = text?.Trim() ?? string.Empty;
        var validation = await ValidateAsync(text, images, ct);
        if (validation.Error != null) return validation.Error;

        // 2. Одна відповідь агента на розмову за раз (друга вкладка отримає 409)
        if (!busy.TryBegin(chatId))
        {
            return ApiResults.Error(StatusCodes.Status409Conflict, "Агент ще відповідає в цій розмові.");
        }

        try
        {
            // 3. Зберігаємо повідомлення користувача ДО виклику агента: модель може думати хвилину-дві,
            //    і якщо щось впаде, повідомлення не загубиться (сторінка покаже «Спробувати ще раз»)
            var imageUrls = await storage.SaveImagesAsync(validation.Images, ct);

            var userMessage = new ChatMessage
            {
                // id генерує сторінка, щоб упізнати своє повідомлення після обриву зв'язку
                Id = IsUsableMessageId(messageId) ? messageId! : Guid.NewGuid().ToString("N"),
                Role = ChatMessage.UserRole,
                Text = text,
                Images = imageUrls,
                CreatedAt = DateTime.UtcNow
            };

            var chat = await storage.UpdateChatAsync(chatId, c =>
            {
                if (c.Messages.Count == 0 && c.Title == ChatSession.DefaultTitle)
                {
                    c.Title = ChatText.TitleFrom(userMessage.Text, userMessage.Images.Count);
                }

                c.Messages.Add(userMessage);
                c.UpdatedAt = userMessage.CreatedAt;
            });

            if (chat == null)
            {
                storage.DeleteImages(imageUrls);
                return ApiResults.ChatNotFound();
            }

            // 4. Агент
            var result = await runner.RunAsync(chatId);

            return result.Status switch
            {
                ReplyStatus.Ok => Results.Ok(new
                {
                    userMessage,
                    assistantMessage = result.AssistantMessage,
                    chat = ChatSummary.From(result.Chat!)
                }),
                // 502 з userMessage і chat: сторінка лишить повідомлення і покаже «Спробувати ще раз»
                ReplyStatus.Failed => Results.Json(new
                {
                    error = "Агент не відповів. Спробуйте ще раз.",
                    userMessage,
                    chat = ChatSummary.From(result.Chat ?? chat)
                }, statusCode: StatusCodes.Status502BadGateway),
                _ => ApiResults.ChatNotFound()
            };
        }
        finally
        {
            busy.End(chatId);
        }
    }

    private static async Task<(IResult? Error, List<ValidatedImage> Images)> ValidateAsync(
        string text, IFormFileCollection files, CancellationToken ct)
    {
        var images = new List<ValidatedImage>();

        if (text.Length == 0 && files.Count == 0)
            return (ApiResults.Error(StatusCodes.Status400BadRequest, "Порожнє повідомлення."), images);

        if (text.Length > ChatLimits.MaxTextLength)
            return (ApiResults.Error(StatusCodes.Status400BadRequest,
                $"Максимум — {ChatLimits.MaxTextLength} символів."), images);

        if (files.Count > ChatLimits.MaxImages)
            return (ApiResults.Error(StatusCodes.Status400BadRequest,
                $"В одне повідомлення можна додати до {ChatLimits.MaxImages} фото."), images);

        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > ChatLimits.MaxImageBytes)
                return (ApiResults.Error(StatusCodes.Status400BadRequest,
                    $"Фото має бути не більше {ChatLimits.MaxImageBytes / 1024 / 1024} МБ."), images);

            var extension = await ImageValidator.DetectExtensionAsync(file, ct);
            if (extension == null)
                return (ApiResults.Error(StatusCodes.Status415UnsupportedMediaType,
                    "Підходять лише фото JPG, PNG, WEBP або GIF."), images);

            images.Add(new ValidatedImage(file, extension));
        }

        return (null, images);
    }

    private static bool IsUsableMessageId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
