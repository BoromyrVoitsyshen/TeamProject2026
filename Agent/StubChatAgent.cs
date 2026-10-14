using AiChat.Api.Domain;

namespace AiChat.Api.Agent;

/// <summary>Заглушка, поки AI-частина не готова. Замінюється в Program.cs.</summary>
public class StubChatAgent : IChatAgent
{
    public async Task<string> ReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)
    {
        // Невелика затримка, щоб на сторінці було видно «друкує…», як зі справжньою моделлю
        await Task.Delay(TimeSpan.FromMilliseconds(600), ct);

        var last = history[^1];
        var photos = last.Images.Count > 0 ? $" і {last.Images.Count} фото" : string.Empty;
        return $"Привіт! Ти написав: «{last.Text}»{photos}. Я поки що заглушка, ШІ ще не підключено.";
    }
}
