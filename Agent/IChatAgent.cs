using AiChat.Api.Domain;

namespace AiChat.Api.Agent;

/// <summary>
/// Точка підключення моделі. Ендпоінти знають лише про цей інтерфейс, тому заміна заглушки
/// на справжню модель — це новий клас + один рядок у Program.cs.
/// </summary>
public interface IChatAgent
{
    /// <param name="history">Уся розмова; останній елемент — нове повідомлення користувача.
    /// Фото — URL на кшталт "/uploads/x.png"; шлях на диску дає ChatStorage.TryGetUploadPath(userId, url).
    /// Фото лежать у папці власника, тож коли агент почне їх читати, йому знадобиться ще й id власника.</param>
    /// <param name="ct">Скасовується через Agent:TimeoutSeconds або при зупинці сервера.</param>
    /// <returns>Текст відповіді. Виняток або порожній рядок = «агент не відповів» (502).</returns>
    Task<string> ReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct);
}
