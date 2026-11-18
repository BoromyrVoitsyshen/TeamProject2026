using System.Globalization;

namespace AiChat.Api.Scheduling;

/// <summary>Час і дати в API — завжди рядками "HH:mm" і "yyyy-MM-dd", незалежно від мови системи.</summary>
public static class Times
{
    public static string Format(TimeOnly time) => time.ToString("HH':'mm", CultureInfo.InvariantCulture);

    public static string Format(DateOnly date) => date.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture);

    /// <summary>Дата і час запису: "2026-11-12T09:30".</summary>
    public static string Format(DateTime value) => value.ToString("yyyy'-'MM'-'dd'T'HH':'mm", CultureInfo.InvariantCulture);

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), "HH':'mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy'-'MM'-'dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
