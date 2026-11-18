namespace AiChat.Api.Agent;

/// <summary>
/// Інструкція і JSON-схема для заповнення підсумку консультації (MECH-86).
/// Схема описує те саме, що клас Domain/ConsultationForm (без службових полів): змінюєте одне — змініть і друге.
/// </summary>
public static class FormPrompt
{
    public const string SchemaName = "consultation_form";

    /// <summary>Типи робіт, з яких модель обирає (сторінка показує їх як підказки).</summary>
    public static readonly string[] WorkTypes =
    [
        "Діагностика", "ТО", "Двигун", "Трансмісія", "Ходова і підвіска", "Гальма", "Рульове керування",
        "Електрика", "Кондиціонер і клімат", "Шини і диски", "Кузов", "Інше"
    ];

    public static readonly string Instructions = $$"""
        Ти заповнюєш підсумок консультації щодо автомобіля за розмовою користувача з асистентом.
        Підсумок прочитає сам користувач і майстер СТО.

        Правила:
        1. Бери факти ЛИШЕ з розмови. Нічого не вигадуй і не домислюй: якщо марки, року, пробігу чи
           іншого поля в розмові немає — став null (для списків — порожній список []).
        2. Чого бракує для точнішої оцінки — коротко перелічи в "missingInfo" (наприклад: "Не вказано рік авто").
        3. "possibleCauses" і "recommendedActions" можна брати з порад асистента в розмові; не додавай нових.
        4. "urgency": "low" — можна планово, "medium" — варто найближчим часом, "high" — якнайшвидше,
           "critical" — не їхати, небезпечно. null — якщо з розмови незрозуміло.
        5. "safeToDrive": true/false лише якщо це випливає з розмови, інакше null. Пояснення — у "safetyNote".
        6. "workTypes" — обирай із цього списку: {{string.Join(", ", WorkTypes)}}.
        7. Пиши українською, коротко: "problemSummary" — 1–2 речення, пункти списків — по одному реченню.
        8. Якщо розмова не про автомобіль, залиш поля порожніми і напиши про це в "missingInfo".
        9. Відповідай ЛИШЕ JSON-об'єктом за схемою, без пояснень і без ```.
        """;

    /// <summary>
    /// JSON-схема для structured output. strict: усі поля обов'язкові, «немає даних» = null,
    /// тож модель не може просто пропустити поле, а мусить явно сказати «невідомо».
    /// </summary>
    public const string Schema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["car", "problemSummary", "symptoms", "possibleCauses", "urgency", "safeToDrive",
                       "safetyNote", "recommendedActions", "workTypes", "missingInfo"],
          "properties": {
            "car": {
              "type": "object",
              "additionalProperties": false,
              "required": ["make", "model", "year", "engine", "fuel", "mileageKm"],
              "properties": {
                "make": { "type": ["string", "null"], "description": "Марка, напр. Volkswagen" },
                "model": { "type": ["string", "null"], "description": "Модель, напр. Golf" },
                "year": { "type": ["integer", "null"], "description": "Рік випуску" },
                "engine": { "type": ["string", "null"], "description": "Двигун, напр. 2.0 TDI" },
                "fuel": { "type": ["string", "null"], "description": "Бензин, дизель, газ, гібрид, електро" },
                "mileageKm": { "type": ["integer", "null"], "description": "Пробіг у кілометрах" }
              }
            },
            "problemSummary": { "type": ["string", "null"], "description": "Суть проблеми в 1–2 реченнях" },
            "symptoms": { "type": "array", "items": { "type": "string" } },
            "possibleCauses": { "type": "array", "items": { "type": "string" } },
            "urgency": { "type": ["string", "null"], "enum": ["low", "medium", "high", "critical", null] },
            "safeToDrive": { "type": ["boolean", "null"] },
            "safetyNote": { "type": ["string", "null"] },
            "recommendedActions": { "type": "array", "items": { "type": "string" } },
            "workTypes": { "type": "array", "items": { "type": "string" } },
            "missingInfo": { "type": "array", "items": { "type": "string" } }
          }
        }
        """;
}
