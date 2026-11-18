using System.Text.Json.Serialization;

namespace AiChat.Api.Domain;

/// <summary>
/// Підсумок консультації (MECH-86): наприкінці розмови модель заповнює цю форму, а користувач
/// може її виправити й показати майстру СТО. Зберігається разом із розмовою (ChatSession.Form).
/// Порожнє поле (null або порожній список) означає «у розмові цього не було» — модель не повинна вигадувати.
/// </summary>
public class ConsultationForm
{
    public const int MaxText = 500;
    public const int MaxListItems = 10;
    public const int MaxListItemText = 300;

    public CarInfo Car { get; set; } = new();

    /// <summary>Суть проблеми в 1–2 реченнях.</summary>
    public string? ProblemSummary { get; set; }

    public List<string> Symptoms { get; set; } = [];

    public List<string> PossibleCauses { get; set; } = [];

    public Urgency? Urgency { get; set; }

    /// <summary>Чи можна їхати своїм ходом. null — з розмови незрозуміло.</summary>
    public bool? SafeToDrive { get; set; }

    public string? SafetyNote { get; set; }

    public List<string> RecommendedActions { get; set; } = [];

    /// <summary>Тип робіт для СТО: «Діагностика», «Гальма», «Ходова» тощо (див. FormPrompt).</summary>
    public List<string> WorkTypes { get; set; } = [];

    /// <summary>Чого бракує, щоб майстер міг точніше оцінити: «не вказано рік авто» тощо.</summary>
    public List<string> MissingInfo { get; set; } = [];

    /// <summary>Скільки повідомлень було в розмові, коли форму заповнила модель — так сторінка бачить, що підсумок застарів.</summary>
    public int BasedOnMessages { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>true — користувач редагував форму після моделі.</summary>
    public bool EditedByUser { get; set; }

    /// <summary>
    /// Приводить форму до допустимого вигляду. Викликається і для відповіді моделі, і для правок
    /// користувача: обом джерелам не довіряємо — обрізаємо довгі тексти, викидаємо порожні рядки,
    /// неможливий рік чи пробіг стають null.
    /// </summary>
    public ConsultationForm Normalize()
    {
        Car ??= new CarInfo();
        Car.Normalize();

        ProblemSummary = CleanText(ProblemSummary, MaxText);
        SafetyNote = CleanText(SafetyNote, MaxText);
        Symptoms = CleanList(Symptoms);
        PossibleCauses = CleanList(PossibleCauses);
        RecommendedActions = CleanList(RecommendedActions);
        WorkTypes = CleanList(WorkTypes);
        MissingInfo = CleanList(MissingInfo);

        if (Urgency is { } urgency && !Enum.IsDefined(urgency)) Urgency = null;
        if (BasedOnMessages < 0) BasedOnMessages = 0;
        return this;
    }

    internal static string? CleanText(string? text, int maxLength)
    {
        var clean = text?.Trim();
        return string.IsNullOrEmpty(clean) ? null : ChatText.Truncate(clean, maxLength);
    }

    private static List<string> CleanList(List<string>? items) =>
        (items ?? [])
            .Select(item => CleanText(item, MaxListItemText))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxListItems)
            .ToList();
}

public class CarInfo
{
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Engine { get; set; }
    public string? Fuel { get; set; }
    public int? MileageKm { get; set; }

    internal void Normalize()
    {
        Make = ConsultationForm.CleanText(Make, 60);
        Model = ConsultationForm.CleanText(Model, 60);
        Engine = ConsultationForm.CleanText(Engine, 60);
        Fuel = ConsultationForm.CleanText(Fuel, 30);
        if (Year is < 1950 || Year > DateTime.UtcNow.Year + 1) Year = null;
        if (MileageKm is < 0 or > 3_000_000) MileageKm = null;
    }
}

/// <summary>Терміновість. У JSON — рядки "low" / "medium" / "high" / "critical" (так їх повертає модель і читає сторінка).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Urgency>))]
public enum Urgency
{
    [JsonStringEnumMemberName("low")] Low,
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("high")] High,
    [JsonStringEnumMemberName("critical")] Critical
}
