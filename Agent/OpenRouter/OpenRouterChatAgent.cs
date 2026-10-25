using System.Net.Http.Json;
using System.Text.Json;
using AiChat.Api.Domain;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent.OpenRouter;

/// <summary>
/// Відповіді від моделей через OpenRouter (https://openrouter.ai/docs).
/// HttpClient приходить з IHttpClientFactory вже налаштованим (адреса, ключ, заголовки) — див. Program.cs.
/// Таймаут, 502 і збереження відповіді робить AgentReplyRunner, тут лише сам виклик моделі.
/// </summary>
public class OpenRouterChatAgent(
    HttpClient http,
    IOptions<AgentOptions> options,
    ILogger<OpenRouterChatAgent> logger) : IChatAgent
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

        // Відносний шлях без "/" на початку: інакше він відкине "/api/v1/" з BaseAddress
        using var response = await http.PostAsJsonAsync("chat/completions", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, ct);
            // 401 — неправильний ключ, 402 — немає кредитів, 429 — вичерпано ліміт безкоштовних запитів
            throw new OpenRouterException((int)response.StatusCode, $"HTTP {(int)response.StatusCode}: {error}");
        }

        var body = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(ct)
                   ?? throw new OpenRouterException(null, "Empty response body");

        if (body.Error != null)
        {
            throw new OpenRouterException(body.Error.Code, body.Error.Message ?? "Unknown OpenRouter error");
        }

        var choice = body.Choices?.FirstOrDefault();
        var text = choice?.Message?.Content;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new OpenRouterException(null,
                $"Model {body.Model} returned no text (finish_reason: {choice?.FinishReason ?? "none"})");
        }

        logger.LogInformation("OpenRouter reply from {Model}, finish_reason {FinishReason}",
            body.Model, choice?.FinishReason);

        return text;
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
    private static string ToModelText(ChatMessage message)
    {
        if (message.Images.Count == 0) return message.Text;

        var photos = string.Join(' ', Enumerable.Repeat("[фото]", message.Images.Count));
        return message.Text.Length == 0 ? photos : $"{message.Text}\n{photos}";
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var raw = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var envelope = JsonSerializer.Deserialize<OpenRouterErrorEnvelope>(raw);
            if (!string.IsNullOrWhiteSpace(envelope?.Error?.Message)) return envelope.Error.Message;
        }
        catch (JsonException)
        {
            // не JSON — покажемо як є
        }

        return raw.Length > 500 ? raw[..500] : raw;
    }
}
