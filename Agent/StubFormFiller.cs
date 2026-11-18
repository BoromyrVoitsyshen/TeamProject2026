using AiChat.Api.Domain;

namespace AiChat.Api.Agent;

/// <summary>Заглушка без ключа OpenRouter: «заповнює» лише суть проблеми з першого повідомлення.</summary>
public class StubFormFiller : IConsultationFormFiller
{
    public async Task<ConsultationForm> FillAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), ct);

        var firstQuestion = history.FirstOrDefault(m => m.Role == ChatMessage.UserRole && m.Text.Length > 0)?.Text;
        return new ConsultationForm
        {
            ProblemSummary = firstQuestion,
            MissingInfo = ["ШІ не підключено (немає Agent:ApiKey) — форму заповнила заглушка. Решту полів заповніть вручну."]
        };
    }
}
