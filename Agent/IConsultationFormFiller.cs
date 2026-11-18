using AiChat.Api.Domain;

namespace AiChat.Api.Agent;

/// <summary>
/// Заповнює підсумок консультації (MECH-86) за розмовою. Як і IChatAgent, ендпоінти знають лише про
/// цей інтерфейс: реалізація з OpenRouter, заглушка без ключа, у майбутньому — локальна модель.
/// </summary>
public interface IConsultationFormFiller
{
    /// <returns>Заповнена форма (ще не нормалізована — це робить ConsultationFormService).</returns>
    /// <exception cref="Exception">Модель не відповіла або відповіла не за схемою → 502.</exception>
    Task<ConsultationForm> FillAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct);
}
