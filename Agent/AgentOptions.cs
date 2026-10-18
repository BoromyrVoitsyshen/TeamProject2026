namespace AiChat.Api.Agent;

/// <summary>
/// Секція "Agent" з конфігурації. ApiKey — лише через dotnet user-secrets локально
/// або змінну середовища Agent__ApiKey у Docker, ніколи не в appsettings.json.
/// </summary>
public class AgentOptions
{
    public string Endpoint {get; set;} = "";
    public string Model {get; set;} = "";
    public string SystemPromptPath {get; set;} = "Agent/Prompts/SystemPrompt.txt";
    public const string SectionName = "Agent";

    public int TimeoutSeconds { get; set; } = 120;

    public string? ApiKey { get; set; }
}
