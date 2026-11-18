using AiChat.Api.Persistence;

namespace AiChat.Api.Scheduling;

/// <summary>Дні тижня: 1 = понеділок … 7 = неділя.</summary>
public static class Weekdays
{
    public static readonly string[] Short = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд"];

    public static int Of(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    public static bool IsValid(int weekday) => weekday is >= 1 and <= 7;
}

/// <summary>Усе, з чого генерується графік одного майстра.</summary>
public sealed record ScheduleInput(
    IReadOnlyList<WorkingDay> Days,
    IReadOnlySet<DateOnly> DaysOff,
    IReadOnlyList<Booking> Bookings,
    int StepMinutes,
    int HorizonDays,
    DateTime Now);

/// <summary>День для клієнта: вільний час, на який можна записатися на послугу.</summary>
public sealed record FreeDay(DateOnly Date, bool IsWorking, List<TimeOnly> Slots);

/// <summary>Клітинка графіка майстра довжиною в один крок (напр. 30 хв).</summary>
public sealed record TimetableCell(TimeOnly Time, string State, int? BookingId);

/// <summary>День у графіку майстра: робочі години, клітинки і записи.</summary>
public sealed record TimetableDay(
    DateOnly Date,
    bool IsWorking,
    bool IsDayOff,
    TimeOnly? Start,
    TimeOnly? End,
    List<TimetableCell> Cells,
    List<Booking> Bookings);

/// <summary>
/// Генерує графік із тижневого розкладу. Самі «слоти» в базі не зберігаються:
/// щоразу рахуються з робочих днів, вихідних і вже зроблених записів. Тому зміна розкладу
/// одразу змінює вільний час, а записи, які вже є, лишаються як були.
/// </summary>
public static class ScheduleGenerator
{
    /// <summary>Записатися можна не пізніше ніж за стільки хвилин до початку.</summary>
    public const int MinLeadMinutes = 30;

    public const int MaxDaysPerRequest = 31;

    public static DateOnly LastBookableDay(DateTime now, int horizonDays) =>
        DateOnly.FromDateTime(now).AddDays(Math.Max(1, horizonDays) - 1);

    /// <summary>
    /// Вільний час на послугу тривалістю <paramref name="durationMinutes"/>: з початку робочого дня з кроком майстра,
    /// так, щоб послуга закінчилась до кінця дня і не перетиналась з іншими записами.
    /// </summary>
    public static List<FreeDay> FreeSlots(ScheduleInput input, int durationMinutes, DateOnly from, int dayCount)
    {
        var result = new List<FreeDay>();
        var today = DateOnly.FromDateTime(input.Now);
        var last = LastBookableDay(input.Now, input.HorizonDays);
        var step = Math.Max(5, input.StepMinutes);
        var earliest = input.Now.AddMinutes(MinLeadMinutes);

        if (from < today) from = today;
        var until = from.AddDays(Math.Clamp(dayCount, 1, MaxDaysPerRequest));

        var busy = input.Bookings
            .Where(b => b.Status == BookingStatus.Active)
            .Select(b => (b.StartsAt, b.EndsAt))
            .ToList();

        for (var date = from; date < until && date <= last; date = date.AddDays(1))
        {
            var hours = HoursFor(input, date);
            if (hours == null)
            {
                result.Add(new FreeDay(date, false, []));
                continue;
            }

            var slots = new List<TimeOnly>();
            var dayEnd = date.ToDateTime(hours.End);

            for (var start = date.ToDateTime(hours.Start); start.AddMinutes(durationMinutes) <= dayEnd; start = start.AddMinutes(step))
            {
                if (start < earliest) continue;

                var end = start.AddMinutes(durationMinutes);
                if (busy.Any(b => b.StartsAt < end && start < b.EndsAt)) continue;

                slots.Add(TimeOnly.FromDateTime(start));
            }

            result.Add(new FreeDay(date, true, slots));
        }

        return result;
    }

    /// <summary>Чи є такий час серед вільних — перевірка перед створенням запису.</summary>
    public static bool IsFree(ScheduleInput input, int durationMinutes, DateOnly date, TimeOnly time) =>
        FreeSlots(input, durationMinutes, date, 1) is [var day] && day.Slots.Contains(time);

    /// <summary>Графік для кабінету майстра: кожен робочий день поділено на клітинки по кроку, зайняті позначено.</summary>
    public static List<TimetableDay> Timetable(ScheduleInput input, DateOnly from, int dayCount)
    {
        var result = new List<TimetableDay>();
        var step = Math.Max(5, input.StepMinutes);
        var until = from.AddDays(Math.Clamp(dayCount, 1, MaxDaysPerRequest));

        for (var date = from; date < until; date = date.AddDays(1))
        {
            var dayBookings = input.Bookings
                .Where(b => b.Status == BookingStatus.Active && DateOnly.FromDateTime(b.StartsAt) == date)
                .OrderBy(b => b.StartsAt)
                .ToList();

            var weekly = input.Days.FirstOrDefault(d => d.Weekday == Weekdays.Of(date));
            var isDayOff = weekly != null && input.DaysOff.Contains(date);
            var cells = new List<TimetableCell>();

            if (weekly != null && !isDayOff)
            {
                var dayEnd = date.ToDateTime(weekly.End);
                for (var start = date.ToDateTime(weekly.Start); start.AddMinutes(step) <= dayEnd; start = start.AddMinutes(step))
                {
                    var end = start.AddMinutes(step);
                    var booking = dayBookings.FirstOrDefault(b => b.StartsAt < end && start < b.EndsAt);
                    var state = booking != null ? "booked" : start < input.Now ? "past" : "free";
                    cells.Add(new TimetableCell(TimeOnly.FromDateTime(start), state, booking?.Id));
                }
            }

            result.Add(new TimetableDay(
                date,
                IsWorking: weekly != null && !isDayOff,
                IsDayOff: isDayOff,
                Start: weekly?.Start,
                End: weekly?.End,
                cells,
                dayBookings));
        }

        return result;
    }

    private static WorkingDay? HoursFor(ScheduleInput input, DateOnly date)
    {
        if (input.DaysOff.Contains(date)) return null;
        return input.Days.FirstOrDefault(d => d.Weekday == Weekdays.Of(date));
    }
}
