using System.Text;
using System.Text.Json;
using AiChat.Api.Domain;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent.OpenRouter;

/// <summary>
/// Заповнює підсумок консультації (MECH-86) через OpenRouter.
/// Спершу просить structured output (JSON за схемою — модель фізично не може відповісти інакше).
/// Не всі безкоштовні моделі це вміють, тому якщо запит відхилено або JSON не розібрався —
/// одна повторна спроба у звичайному режимі, де формат задає лише інструкція.
/// </summary>
public class OpenRouterFormFiller(
    OpenRouterClient client,
    IOptions<AgentOptions> options,
    ILogger<OpenRouterFormFiller> logger) : IConsultationFormFiller
{
    private static readonly JsonElement ResponseFormat = JsonSerializer.SerializeToElement(new
    {
        type = "json_schema",
        json_schema = new
        {
            name = FormPrompt.SchemaName,
            strict = true,
            schema = JsonDocument.Parse(FormPrompt.Schema).RootElement
        }
    });

    // Ті самі налаштування, що в ASP.NET для API: camelCase, без урахування регістру
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentOptions _options = options.Value;

    public async Task<ConsultationForm> FillAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)
    {
        var messages = new List<OpenRouterMessage>
        {
            new("system", FormPrompt.Instructions),
            new("user", BuildTranscript(history, _options.FormMaxMessages))
        };

        try
        {
            return await TryFillAsync(messages, structured: true, ct);
        }
        catch (Exception ex) when (IsWorthRetryingWithoutSchema(ex))
        {
            logger.LogWarning("Structured output failed ({Error}), retrying with JSON by instructions only", ex.Message);
            return await TryFillAsync(messages, structured: false, ct);
        }
    }

    private async Task<ConsultationForm> TryFillAsync(List<OpenRouterMessage> messages, bool structured, CancellationToken ct)
    {
        var request = new OpenRouterRequest
        {
            Models = _options.Models,
            Messages = messages,
            MaxTokens = _options.FormMaxTokens,
            // Низька температура: тут потрібне точне перенесення фактів, а не творчість
            Temperature = 0.1,
            ResponseFormat = structured ? ResponseFormat : null,
            // Лише ті провайдери, що підтримують response_format, інакше параметр могли б проігнорувати
            Provider = structured ? new OpenRouterProviderPreferences(RequireParameters: true) : null
        };

        var completion = await client.CompleteAsync(request, ct);
        return ParseForm(completion.Text);
    }

    // 400/404 — модель або провайдер не підтримує structured output; JsonException — відповідь не за схемою.
    // 401, 429, таймаут повторювати сенсу немає: друга спроба впаде так само й ще з'їсть ліміт запитів
    private static bool IsWorthRetryingWithoutSchema(Exception ex) =>
        ex is JsonException || ex is OpenRouterException { StatusCode: 400 or 404 };

    /// <summary>Розмова як текст для моделі: «Користувач: … / Асистент: …», останні <paramref name="maxMessages"/> повідомлень.</summary>
    public static string BuildTranscript(IReadOnlyList<ChatMessage> history, int maxMessages)
    {
        var text = new StringBuilder("Розмова:\n\n");
        foreach (var message in history.TakeLast(Math.Max(1, maxMessages)))
        {
            var who = message.Role == ChatMessage.AssistantRole ? "Асистент" : "Користувач";
            text.Append(who).Append(": ").Append(OpenRouterChatAgent.ToModelText(message)).Append("\n\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// Дістає форму з відповіді моделі. Навіть у звичайному режимі моделі часто обгортають JSON у ```json
    /// або додають фразу перед ним — беремо текст від першої «{» до останньої «}».
    /// </summary>
    public static ConsultationForm ParseForm(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("Model reply contains no JSON object.");

        return JsonSerializer.Deserialize<ConsultationForm>(text[start..(end + 1)], JsonOptions)
               ?? throw new JsonException("Model returned null instead of a form.");
    }
}
