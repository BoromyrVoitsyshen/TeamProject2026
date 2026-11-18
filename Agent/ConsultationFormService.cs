using AiChat.Api.Domain;
using AiChat.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace AiChat.Api.Agent;

public enum FormFillStatus
{
    Ok,
    /// <summary>У розмові ще немає повідомлень користувача — підсумовувати нічого → 400.</summary>
    EmptyChat,
    /// <summary>Модель не відповіла, не вклалась у таймаут або повернула не форму → 502.</summary>
    Failed,
    /// <summary>Розмови немає (або її видалили, поки модель думала) → 404.</summary>
    ChatGone
}

public record FormFillResult(FormFillStatus Status, ConsultationForm? Form = null);

/// <summary>
/// Логіка підсумку консультації (MECH-86): заповнити моделлю або зберегти правки користувача.
/// Ендпоінти лише перетворюють результат на HTTP-коди — так само, як AgentReplyRunner для відповідей.
/// </summary>
public class ConsultationFormService(
    IConsultationFormFiller filler,
    ChatStorage storage,
    IOptions<AgentOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<ConsultationFormService> logger)
{
    public async Task<FormFillResult> FillAsync(int userId, string chatId)
    {
        var chat = await storage.GetChatAsync(userId, chatId);
        if (chat == null) return new FormFillResult(FormFillStatus.ChatGone);
        if (!chat.Messages.Any(m => m.Role == ChatMessage.UserRole)) return new FormFillResult(FormFillStatus.EmptyChat);

        // Як і для відповіді в чаті: таймаут і зупинка сервера, але не закрита вкладка —
        // якщо користувач перезавантажив сторінку, заповнена форма все одно збережеться
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds)));

        ConsultationForm form;
        try
        {
            form = await filler.FillAsync(chat.Messages, cts.Token).WaitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not fill the consultation form for chat {ChatId}", chatId);
            return new FormFillResult(FormFillStatus.Failed);
        }

        var now = DateTime.UtcNow;
        form.Normalize();
        form.BasedOnMessages = chat.Messages.Count;
        form.GeneratedAt = now;
        form.UpdatedAt = now;
        form.EditedByUser = false;

        // Під замком розмови записуємо лише форму: повідомлення, які могли прийти за цей час, не зачіпаємо
        var updated = await storage.UpdateChatAsync(userId, chatId, c => c.Form = form);
        return updated == null ? new FormFillResult(FormFillStatus.ChatGone) : new FormFillResult(FormFillStatus.Ok, form);
    }

    /// <summary>Зберігає форму, яку виправив користувач. null — розмови немає.</summary>
    public async Task<ConsultationForm?> SaveEditedAsync(int userId, string chatId, ConsultationForm edited)
    {
        edited.Normalize();

        var updated = await storage.UpdateChatAsync(userId, chatId, chat =>
        {
            // Службові поля не беремо з запиту: коли й на основі чого форму заповнила модель — знає лише сервер
            edited.BasedOnMessages = chat.Form?.BasedOnMessages ?? chat.Messages.Count;
            edited.GeneratedAt = chat.Form?.GeneratedAt ?? DateTime.UtcNow;
            edited.UpdatedAt = DateTime.UtcNow;
            edited.EditedByUser = true;
            chat.Form = edited;
        });

        return updated?.Form;
    }
}
