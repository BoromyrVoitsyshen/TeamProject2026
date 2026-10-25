namespace AiChat.Api.Agent;

/// <summary>
/// Секція "Agent" з конфігурації. ApiKey — лише через dotnet user-secrets локально
/// або змінну середовища Agent__ApiKey у Docker, ніколи не в appsettings.json.
/// </summary>
public class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Скільки секунд чекати на відповідь моделі, далі — 502 і «Спробувати ще раз».</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Ключ OpenRouter. Порожній — працює заглушка StubChatAgent.</summary>
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    /// <summary>
    /// Моделі в порядку пріоритету. Якщо перша недоступна або вичерпала ліміт,
    /// OpenRouter сам переходить до наступної. Тримайте тут лише ":free"-моделі,
    /// інакше запасна платна модель спише гроші.
    /// </summary>
    public List<string> Models { get; set; } = [];

    /// <summary>Скільки останніх повідомлень розмови надсилати моделі (обрізання довгої історії).</summary>
    public int MaxHistoryMessages { get; set; } = 20;

    /// <summary>Обмеження довжини відповіді в токенах — короткі відповіді й менше витрат ліміту.</summary>
    public int MaxTokens { get; set; } = 800;

    public double Temperature { get; set; } = 0.5;

    /// <summary>Необов'язкові заголовки для OpenRouter: сайт і назва застосунку (лише ASCII).</summary>
    public string? SiteUrl { get; set; }

    public string AppTitle { get; set; } = "AiChat TeamProject2026";

    /// <summary>
    /// При старті перевірити моделі зі списку через {BaseUrl}models: чи існують, чи відповідають текстом,
    /// чи безкоштовні. Лише попередження в лог, застосунок не зупиняється. Вимкнути — для роботи без інтернету.
    /// </summary>
    public bool CheckModelsOnStartup { get; set; } = true;
}
