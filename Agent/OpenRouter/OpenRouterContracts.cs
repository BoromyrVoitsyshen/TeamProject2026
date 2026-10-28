using System.Text.Json.Serialization;

namespace AiChat.Api.Agent.OpenRouter;

// Формат OpenAI Chat Completions, який приймає OpenRouter: POST {BaseUrl}chat/completions.
// Описуємо лише поля, які нам потрібні; решту JSON серіалізатор просто пропустить.

public record OpenRouterRequest
{
    [JsonPropertyName("models")]
    public required IReadOnlyList<string> Models { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<OpenRouterMessage> Messages { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
}

public record OpenRouterMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

// content може бути null (наприклад, модель «думала» і не встигла відповісти до max_tokens)
public record OpenRouterResponseMessage(
    [property: JsonPropertyName("content")] string? Content);

public record OpenRouterResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public List<OpenRouterChoice>? Choices { get; init; }

    // OpenRouter може повернути помилку і з кодом 200 (наприклад, провайдер упав посеред генерації)
    [JsonPropertyName("error")]
    public OpenRouterError? Error { get; init; }
}

public record OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterResponseMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public record OpenRouterErrorEnvelope
{
    [JsonPropertyName("error")]
    public OpenRouterError? Error { get; init; }
}

public record OpenRouterError
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>Модель не відповіла: код HTTP від OpenRouter і його пояснення — для логів.</summary>
public class OpenRouterException(int? statusCode, string message) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
