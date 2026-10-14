using AiChat.Api.Agent;
using AiChat.Api.Domain;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.RetryReply;

/// <summary>Кнопка «Спробувати ще раз»: агент відповідає на останнє повідомлення користувача.</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats/{id}/reply", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        string id,
        ChatStorage storage,
        ChatBusyTracker busy,
        AgentReplyRunner runner)
    {
        var chat = await storage.GetChatAsync(id);
        if (chat == null) return ApiResults.ChatNotFound();

        if (!busy.TryBegin(id))
        {
            return ApiResults.Error(StatusCodes.Status409Conflict, "Агент ще відповідає в цій розмові.");
        }

        try
        {
            // Перевіряємо вже ПІСЛЯ TryBegin: інакше між перевіркою і TryBegin інша вкладка могла б
            // встигнути отримати відповідь, і ми б відповіли двічі
            chat = await storage.GetChatAsync(id);
            if (chat == null) return ApiResults.ChatNotFound();

            if (chat.Messages.LastOrDefault()?.Role != ChatMessage.UserRole)
            {
                return ApiResults.Error(StatusCodes.Status409Conflict, "Агент уже відповів у цій розмові.");
            }

            var result = await runner.RunAsync(id);

            return result.Status switch
            {
                ReplyStatus.Ok => Results.Ok(new
                {
                    assistantMessage = result.AssistantMessage,
                    chat = ChatSummary.From(result.Chat!)
                }),
                ReplyStatus.Failed => ApiResults.Error(StatusCodes.Status502BadGateway, "Агент не відповів. Спробуйте ще раз."),
                _ => ApiResults.ChatNotFound()
            };
        }
        finally
        {
            busy.End(id);
        }
    }
}
