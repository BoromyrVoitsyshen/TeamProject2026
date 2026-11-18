namespace AiChat.Api.Scheduling;

/// <summary>
/// «Зараз» у часовому поясі майстерень. Розклад і записи зберігаються в місцевому часі
/// (майстер працює 09:00–18:00 за київським часом, де б не стояв сервер), тому сервер
/// рахує «зараз» не за UTC і не за поясом своєї машини, а за Booking:TimeZone.
/// </summary>
public class BookingClock
{
    private readonly TimeZoneInfo _zone;

    public BookingClock(IConfiguration configuration, ILogger<BookingClock> logger)
    {
        var id = configuration["Booking:TimeZone"];
        _zone = FindZone(id);
        logger.LogInformation("Booking time zone: {Zone}", _zone.Id);
    }

    /// <summary>Місцевий час майстерні (DateTimeKind.Unspecified — так само, як час записів у базі).</summary>
    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone), DateTimeKind.Unspecified);

    public DateOnly Today => DateOnly.FromDateTime(Now);

    private static TimeZoneInfo FindZone(string? configured)
    {
        // "Europe/Kyiv" — нова назва в базі часових поясів; на старих системах є лише "Europe/Kiev",
        // а на Windows без ICU — "FLE Standard Time"
        string[] candidates = string.IsNullOrWhiteSpace(configured)
            ? ["Europe/Kyiv", "Europe/Kiev", "FLE Standard Time"]
            : [configured, "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time"];

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}
