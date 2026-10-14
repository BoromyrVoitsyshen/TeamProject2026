namespace AiChat.Api.Domain;

public class ChatSession
{
    public const string DefaultTitle = "Нова розмова";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = DefaultTitle;
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessage
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Короткий опис розмови для панелі зліва. Ту саму форму рахує summarize() у wwwroot/chat.js,
/// тому правила (preview до 120 символів, «Фото» без тексту) мають збігатися.
/// </summary>
public record ChatSummary(string Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, string Preview)
{
    public const int PreviewLength = 120;

    public static ChatSummary From(ChatSession chat)
    {
        var last = chat.Messages.LastOrDefault();
        var preview = last switch
        {
            null => string.Empty,
            { Text.Length: > 0 } => last.Text,
            { Images.Count: > 0 } => "Фото",
            _ => string.Empty
        };

        return new ChatSummary(chat.Id, chat.Title, chat.CreatedAt, chat.UpdatedAt, ChatText.Truncate(preview, PreviewLength));
    }
}

/// <summary>Правила для тексту розмови, спільні для кількох ендпоінтів.</summary>
public static class ChatText
{
    public const int TitleLength = 48;
    public const int MaxTitleLength = 100;

    /// <summary>
    /// Назва розмови з першого повідомлення — те саме, що titleFrom() у wwwroot/chat.js:
    /// пробіли зводимо до одного, до 48 символів, далі «…»; без тексту — «Фото».
    /// </summary>
    public static string TitleFrom(string text, int imageCount)
    {
        var line = NormalizeSpaces(text);
        if (line.Length == 0) return imageCount > 0 ? "Фото" : ChatSession.DefaultTitle;
        return line.Length > TitleLength ? Truncate(line, TitleLength).TrimEnd() + "…" : line;
    }

    public static string NormalizeSpaces(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Обрізає рядок, не розриваючи емодзі (сурогатну пару) навпіл.</summary>
    public static string Truncate(string text, int length)
    {
        if (text.Length <= length) return text;
        if (char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length];
    }
}
