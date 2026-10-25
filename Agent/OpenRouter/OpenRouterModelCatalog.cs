using System.Text.Json.Serialization;

namespace AiChat.Api.Agent.OpenRouter;

// Відповідь GET {BaseUrl}models (https://openrouter.ai/api/v1/models).
// Ollama та інші OpenAI-сумісні сервери віддають той самий список "data" з "id",
// але без architecture і pricing — тому ці поля необов'язкові.

public record ModelCatalogResponse
{
    [JsonPropertyName("data")]
    public List<ModelCatalogEntry> Data { get; init; } = [];
}

public record ModelCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("architecture")]
    public ModelArchitecture? Architecture { get; init; }

    [JsonPropertyName("pricing")]
    public ModelPricing? Pricing { get; init; }
}

public record ModelArchitecture
{
    [JsonPropertyName("output_modalities")]
    public List<string>? OutputModalities { get; init; }
}

/// <summary>Ціни OpenRouter приходять рядками в доларах за токен, наприклад "0" або "0.0000025".</summary>
public record ModelPricing
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("completion")]
    public string? Completion { get; init; }
}
