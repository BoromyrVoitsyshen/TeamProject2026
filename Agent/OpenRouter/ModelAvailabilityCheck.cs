using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent.OpenRouter;

/// <summary>
/// Один раз при старті перевіряє моделі з Agent:Models за списком {BaseUrl}models.
/// На відміну від AgentOptionsValidator, тут лише попередження: перевірка залежить від мережі,
/// і сайт має запускатися навіть без інтернету (тоді працюватимуть повідомлення про помилки агента).
/// </summary>
public class ModelAvailabilityCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    ILogger<ModelAvailabilityCheck> logger) : BackgroundService
{
    public const string HttpClientName = "ModelCatalog";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // BackgroundService не затримує старт лише після першого справжнього await —
        // Task.Yield гарантує, що сайт почне приймати запити, не чекаючи на OpenRouter
        await Task.Yield();

        var agent = options.Value;
        if (!agent.CheckModelsOnStartup) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        // Клієнт з фабрики, а не через конструктор: BackgroundService — singleton,
        // і довгоживучий HttpClient у ньому не оновлював би з'єднання (та сама «captive dependency»)
        var http = httpClientFactory.CreateClient(HttpClientName);
        var catalogUrl = new Uri(http.BaseAddress!, "models");

        ModelCatalogResponse? catalog;
        try
        {
            catalog = await http.GetFromJsonAsync<ModelCatalogResponse>(catalogUrl, timeout.Token);
        }
        catch (Exception ex)
        {
            // Ловимо все: необроблений виняток у BackgroundService за замовчуванням ЗУПИНЯЄ весь застосунок
            if (stoppingToken.IsCancellationRequested) return;
            logger.LogWarning("Could not check Agent:Models at {Url}: {Error} The chat may still work.",
                catalogUrl, ex.Message);
            return;
        }

        if (catalog == null || catalog.Data.Count == 0)
        {
            logger.LogWarning("Model list at {Url} is empty, Agent:Models were not checked.", catalogUrl);
            return;
        }

        var problems = FindProblems(agent.Models, catalog.Data);
        foreach (var problem in problems)
        {
            logger.LogWarning("Agent:Models: {Problem}", problem);
        }

        if (problems.Count == 0)
        {
            logger.LogInformation("Agent:Models checked: {Models} — all exist, reply with text and are free.",
                string.Join(", ", agent.Models));
        }
    }

    /// <summary>Чиста функція без мережі — її легко покрити unit-тестами.</summary>
    public static List<string> FindProblems(IEnumerable<string> models, IReadOnlyList<ModelCatalogEntry> catalog)
    {
        var byId = catalog
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var model in models)
        {
            if (!byId.TryGetValue(model, out var entry))
            {
                problems.Add($"'{model}' was not found. Check the exact name on openrouter.ai/models.");
                continue;
            }

            // Немає architecture (наприклад, Ollama) — вважаємо, що це чат-модель
            var outputs = entry.Architecture?.OutputModalities;
            if (outputs is { Count: > 0 } && !outputs.Contains("text", StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"'{model}' does not reply with text (output: {string.Join(", ", outputs)}). " +
                             "Embedding or rerank models cannot be used for chat.");
            }

            if (IsPaid(entry.Pricing))
            {
                problems.Add($"'{model}' is not free (prompt {entry.Pricing!.Prompt}, completion {entry.Pricing.Completion} " +
                             "USD per token) — requests will use account credits.");
            }
        }

        return problems;
    }

    // Невідома ціна (немає pricing або дивний формат) — не попереджаємо, щоб не шуміти даремно
    private static bool IsPaid(ModelPricing? pricing) =>
        pricing != null && (IsPositive(pricing.Prompt) || IsPositive(pricing.Completion));

    private static bool IsPositive(string? price) =>
        decimal.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0;
}
