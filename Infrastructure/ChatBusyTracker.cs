using System.Collections.Concurrent;

namespace AiChat.Api.Infrastructure;

/// <summary>
/// Пам'ятає, у яких розмовах агент зараз відповідає. Звідси беруться
/// "busy" у GET /api/chats/{id} і 409 на друге повідомлення в ту саму розмову.
/// Стан живе в пам'яті процесу: після перезапуску сервера жодна розмова не "зайнята" — так і має бути,
/// бо незавершені виклики агента теж зникли.
/// </summary>
public class ChatBusyTracker
{
    private readonly ConcurrentDictionary<string, byte> _busy = new();

    /// <summary>Атомарно позначає розмову зайнятою. false — вона вже зайнята іншим запитом.</summary>
    public bool TryBegin(string chatId) => _busy.TryAdd(chatId, 0);

    public void End(string chatId) => _busy.TryRemove(chatId, out _);

    public bool IsBusy(string chatId) => _busy.ContainsKey(chatId);
}
