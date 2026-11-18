using System.Net.Http.Json;
using System.Text.Json;

namespace AiChat.Api.Agent.OpenRouter;

/// <summary>Що повернула модель: текст, яка модель реально відповіла і чому зупинилась.</summary>
public record OpenRouterCompletion(string Text, string? Model, string? FinishReason);

/// <summary>
/// Один запит до POST {BaseUrl}chat/completions і розбір помилок. Спільний для відповіді в чаті
/// (OpenRouterChatAgent) і заповнення форми (OpenRouterFormFiller) — щоб обробка 401/429/порожньої
/// відповіді була в одному місці.
/// HttpClient приходить з IHttpClientFactory вже налаштованим (адреса, ключ, заголовки) — див. Program.cs.
/// </summary>
public class OpenRouterClient(HttpClient http, ILogger<OpenRouterClient> logger)
{
    public async Task<OpenRouterCompletion> CompleteAsync(OpenRouterRequest request, CancellationToken ct)
    {
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

        if (choice?.FinishReason == "length")
        {
            // Відповідь обрізана лімітом токенів: у чаті це обірване речення, у формі — зламаний JSON
            logger.LogWarning("Reply from {Model} was cut by max_tokens ({MaxTokens}). Consider raising Agent:MaxTokens.",
                body.Model, request.MaxTokens);
        }

        return new OpenRouterCompletion(text, body.Model, choice?.FinishReason);
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
