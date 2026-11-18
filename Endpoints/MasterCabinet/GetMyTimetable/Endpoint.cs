using AiChat.Api.Accounts;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.MasterCabinet.GetMyTimetable;

/// <summary>
/// Згенерований графік майстра на кілька днів: робочі години поділено на клітинки за кроком запису,
/// у кожній — вільно / зайнято / минуло, а під днем — список записів з іменами клієнтів.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my/master/timetable", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct, string? from = null, int? days = null)
    {
        var (profile, error) = await MasterProfileView.LoadOwnAsync(http, db, track: false, ct);
        if (error != null) return error;

        var start = clock.Today;
        if (from != null && !Times.TryParseDate(from, out start))
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Дата має бути у форматі РРРР-ММ-ДД.");

        var dayCount = Math.Clamp(days ?? 7, 1, ScheduleGenerator.MaxDaysPerRequest);
        var input = await ScheduleData.LoadAsync(db, profile!, clock.Now, start, start.AddDays(dayCount), ct);
        var timetable = ScheduleGenerator.Timetable(input, start, dayCount);

        var clientIds = timetable.SelectMany(d => d.Bookings).Select(b => b.ClientId).Distinct().ToList();
        var clients = await db.Users.AsNoTracking()
            .Where(u => clientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return Results.Ok(new
        {
            slotStepMinutes = profile!.SlotStepMinutes,
            days = timetable.Select(d => new
            {
                date = Times.Format(d.Date),
                weekday = Weekdays.Short[Weekdays.Of(d.Date) - 1],
                d.IsWorking,
                d.IsDayOff,
                start = d.Start is { } s ? Times.Format(s) : null,
                end = d.End is { } e ? Times.Format(e) : null,
                freeCount = d.Cells.Count(c => c.State == "free"),
                cells = d.Cells.Select(c => new { time = Times.Format(c.Time), c.State, c.BookingId }),
                bookings = d.Bookings.Select(b => new
                {
                    b.Id,
                    start = Times.Format(TimeOnly.FromDateTime(b.StartsAt)),
                    end = Times.Format(TimeOnly.FromDateTime(b.EndsAt)),
                    service = b.ServiceName,
                    client = CurrentUser.PublicName(clients.GetValueOrDefault(b.ClientId, ""))
                })
            })
        });
    }
}
