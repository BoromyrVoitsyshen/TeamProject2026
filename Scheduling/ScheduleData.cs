using AiChat.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Scheduling;

/// <summary>Завантажує з бази все, з чого генерується графік майстра на проміжок дат.</summary>
public static class ScheduleData
{
    public static async Task<ScheduleInput> LoadAsync(
        AppDbContext db, MasterProfile master, DateTime now, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var days = await db.WorkingDays.AsNoTracking()
            .Where(d => d.MasterId == master.Id)
            .ToListAsync(ct);

        var daysOff = await db.DaysOff.AsNoTracking()
            .Where(d => d.MasterId == master.Id && d.Date >= from && d.Date <= until)
            .Select(d => d.Date)
            .ToListAsync(ct);

        var windowStart = from.ToDateTime(TimeOnly.MinValue);
        var windowEnd = until.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.MasterId == master.Id
                        && b.Status == BookingStatus.Active
                        && b.StartsAt < windowEnd
                        && b.EndsAt > windowStart)
            .ToListAsync(ct);

        return new ScheduleInput(days, daysOff.ToHashSet(), bookings, master.SlotStepMinutes, master.BookingHorizonDays, now);
    }
}
