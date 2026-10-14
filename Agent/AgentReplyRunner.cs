using AiChat.Api.Domain;
using AiChat.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent;

public enum ReplyStatus
{
    Ok,
    /// <summary>Агент упав, не вклався в таймаут або повернув порожню відповідь → 502.</summary>
    Failed,
    /// <summary>Розмову видалили, поки агент думав → 404.</summary>
    ChatGone
}

public record ReplyResult(ReplyStatus Status, ChatMessage? AssistantMessage = null, ChatSession? Chat = null);

/// <summary>
/// Спільна логіка «попросити агента відповісти і зберегти відповідь» для
/// POST …/messages і POST …/reply. Викликач сам позначає розмову зайнятою (ChatBusyTracker).
/// </summary>
public class AgentReplyRunner(
    IChatAgent agent,
    ChatStorage storage,
    IOptions<AgentOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<AgentReplyRunner> logger)
{
    public async Task<ReplyResult> RunAsync(string chatId)
    {
        var chat = await storage.GetChatAsync(chatId);
        if (chat == null) return new ReplyResult(ReplyStatus.ChatGone);

        var timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds));

        // Свідомо НЕ прив'язуємо до HttpContext.RequestAborted: якщо користувач перезавантажив сторінку,
        // відповідь має дописатися в розмову і з'явитися сама (сторінка опитує busy).
        // Скасовуємо лише за таймаутом або коли сервер зупиняється.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        cts.CancelAfter(timeout);

        string reply;
        try
        {
            // WaitAsync гарантує таймаут, навіть якщо реалізація агента ігнорує CancellationToken
            reply = await agent.ReplyAsync(chat.Messages, cts.Token).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogWarning("Agent did not reply in chat {ChatId} within {Timeout}", chatId, timeout);
            return new ReplyResult(ReplyStatus.Failed, Chat: chat);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent failed in chat {ChatId}", chatId);
            return new ReplyResult(ReplyStatus.Failed, Chat: chat);
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            logger.LogWarning("Agent returned an empty reply in chat {ChatId}", chatId);
            return new ReplyResult(ReplyStatus.Failed, Chat: chat);
        }

        var assistantMessage = new ChatMessage
        {
            Role = ChatMessage.AssistantRole,
            Text = reply.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        // Перечитуємо розмову під замком: за ці секунди її могли перейменувати
        var updated = await storage.UpdateChatAsync(chatId, c =>
        {
            c.Messages.Add(assistantMessage);
            c.UpdatedAt = assistantMessage.CreatedAt;
        });

        return updated == null
            ? new ReplyResult(ReplyStatus.ChatGone)
            : new ReplyResult(ReplyStatus.Ok, assistantMessage, updated);
    }
}
