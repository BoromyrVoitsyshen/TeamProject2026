using AiChat.Api.Domain;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent.OpenRouter;

/// <summary>
/// Відповіді в чаті від моделей через OpenRouter (https://openrouter.ai/docs).
/// Сам HTTP-запит і розбір помилок — в OpenRouterClient. Таймаут, 502 і збереження відповіді
/// робить AgentReplyRunner, тут лише побудова запиту для чату.
/// </summary>
public class OpenRouterChatAgent(OpenRouterClient client, IOptions<AgentOptions> options) : IChatAgent
{
    private readonly AgentOptions _options = options.Value;

    public async Task<string> ReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)
    {
        var request = new OpenRouterRequest
        {
            Models = _options.Models,
            Messages = BuildMessages(history, _options.MaxHistoryMessages),
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature
        };

        var completion = await client.CompleteAsync(request, ct);
        return completion.Text;
    }

    /// <summary>
    /// Системний промпт + останні <paramref name="maxHistory"/> повідомлень розмови.
    /// Обрізаємо, бо кожен запит несе всю історію: довгий чат = повільніше, дорожче і
    /// зрештою перевищення контексту моделі.
    /// </summary>
    public static List<OpenRouterMessage> BuildMessages(IReadOnlyList<ChatMessage> history, int maxHistory)
    {
        var recent = history.TakeLast(Math.Max(1, maxHistory))
            // Історія після обрізання має починатися з повідомлення користувача —
            // деякі моделі відхиляють розмову, що починається з відповіді асистента
            .SkipWhile(m => m.Role != ChatMessage.UserRole);

        var messages = new List<OpenRouterMessage> { new("system", SystemPrompt.Text) };

        foreach (var message in recent)
        {
            var role = message.Role == ChatMessage.AssistantRole ? "assistant" : "user";
            messages.Add(new OpenRouterMessage(role, ToModelText(message)));
        }

        return messages;
    }

    // Фото моделі поки не передаємо — лише позначку, щоб вона знала, що фото було (див. SystemPrompt)
    internal static string ToModelText(ChatMessage message)
    {
        if (message.Images.Count == 0) return message.Text;

        var photos = string.Join(' ', Enumerable.Repeat("[фото]", message.Images.Count));
        return message.Text.Length == 0 ? photos : $"{message.Text}\n{photos}";
    }
}
