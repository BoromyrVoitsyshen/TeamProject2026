using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent;

/// <summary>
/// Перевіряє секцію "Agent" при старті застосунку (див. ValidateOnStart у Program.cs).
/// Неправильна конфігурація — помилка, яка сама не зникне, тому краще одразу не запуститися
/// з чітким повідомленням, ніж віддавати 502 на кожне повідомлення в чаті.
/// </summary>
public class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    /// <summary>OpenRouter відхиляє запит, якщо в масиві "models" більше 3 моделей (HTTP 400).</summary>
    public const int MaxModels = 3;

    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        if (options.TimeoutSeconds <= 0)
            errors.Add($"Agent:TimeoutSeconds must be greater than 0 (now {options.TimeoutSeconds}).");
        if (options.MaxTokens <= 0)
            errors.Add($"Agent:MaxTokens must be greater than 0 (now {options.MaxTokens}).");
        if (options.MaxHistoryMessages <= 0)
            errors.Add($"Agent:MaxHistoryMessages must be greater than 0 (now {options.MaxHistoryMessages}).");
        if (options.Temperature is < 0 or > 2)
            errors.Add($"Agent:Temperature must be between 0 and 2 (now {options.Temperature}).");

        // Без ключа працює заглушка — налаштування моделей тоді не використовуються, перевіряти їх нема сенсу
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            ValidateModelSettings(options, errors);
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateModelSettings(AgentOptions options, List<string> errors)
    {
        if (options.ApiKey != options.ApiKey!.Trim())
            errors.Add("Agent:ApiKey has leading or trailing spaces — check how it was copied.");

        if (options.Models.Count is 0 or > MaxModels)
            errors.Add($"Agent:Models must contain 1–{MaxModels} models (OpenRouter limit), now {options.Models.Count}: " +
                       $"[{string.Join(", ", options.Models)}]. Remember that user-secrets and appsettings.json " +
                       "arrays are merged by index.");

        if (options.Models.Any(string.IsNullOrWhiteSpace))
            errors.Add("Agent:Models contains an empty model name.");

        var duplicates = options.Models.GroupBy(m => m).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            errors.Add($"Agent:Models contains duplicates: {string.Join(", ", duplicates)}.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl) ||
            baseUrl.Scheme is not ("http" or "https"))
            errors.Add($"Agent:BaseUrl must be an absolute http(s) URL (now '{options.BaseUrl}').");
    }
}
