using AiChat.Api.Accounts;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.MasterCabinet.SaveMyProfile;

public record ServiceInput(int ServiceId, int Price, int DurationMinutes);

public record DayInput(int Weekday, string? Start, string? End);

public record SaveProfileRequest(
    string? DisplayName,
    string? Description,
    string? City,
    string? Address,
    string? Phone,
    int ExperienceYears,
    int SlotStepMinutes,
    int BookingHorizonDays,
    bool IsPublished,
    List<ServiceInput>? Services,
    List<DayInput>? Days,
    List<string>? DaysOff);

/// <summary>
/// Майстер зберігає свій профіль: дані, послуги з цінами й тривалістю, тижневий розклад
/// (напр. Ср, Чт, Пт 09:00–18:00), крок запису, на скільки днів наперед приймати записи і вихідні дати.
/// З цього сайт сам генерує графік вільного часу. Записи, які вже є, зміна розкладу не скасовує.
/// </summary>
public class Endpoint : IEndpoint
{
    public static readonly int[] AllowedSteps = [15, 20, 30, 45, 60];
    public const int MaxHorizonDays = 60;
    public const int MaxDaysOff = 60;
    public const int MaxPrice = 1_000_000;
    public const int MinDuration = 15;
    public const int MaxDuration = 600;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/my/master", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        SaveProfileRequest request, HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct)
    {
        var (profile, error) = await MasterProfileView.LoadOwnAsync(http, db, track: true, ct);
        if (error != null) return error;

        // ── Перевірка ──
        var name = AccountRules.NormalizeText(request.DisplayName);
        if (name.Length is < 2 or > 80) return Bad("Ім'я або назва — від 2 до 80 символів.");

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length > 2000) return Bad("Опис — до 2000 символів.");

        var city = AccountRules.NormalizeText(request.City);
        if (city.Length > 60) return Bad("Місто — до 60 символів.");

        var address = AccountRules.NormalizeText(request.Address);
        if (address.Length > 200) return Bad("Адреса — до 200 символів.");

        var (phone, phoneError) = AccountRules.NormalizePhone(request.Phone);
        if (phoneError != null) return Bad(phoneError);

        if (request.ExperienceYears is < 0 or > 60) return Bad("Стаж — від 0 до 60 років.");
        if (!AllowedSteps.Contains(request.SlotStepMinutes))
            return Bad($"Крок запису — {string.Join(", ", AllowedSteps)} хвилин.");
        if (request.BookingHorizonDays is < 1 or > MaxHorizonDays)
            return Bad($"Записи наперед — від 1 до {MaxHorizonDays} днів.");

        var knownServices = await db.ServiceTypes.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var services = request.Services ?? [];
        if (services.Select(s => s.ServiceId).Distinct().Count() != services.Count)
            return Bad("Кожну послугу можна додати лише раз.");
        foreach (var service in services)
        {
            if (!knownServices.Contains(service.ServiceId)) return Bad("Невідома послуга — оновіть сторінку.");
            if (service.Price is < 1 or > MaxPrice) return Bad("Ціна послуги — від 1 до 1 000 000 грн.");
            if (service.DurationMinutes is < MinDuration or > MaxDuration || service.DurationMinutes % 5 != 0)
                return Bad($"Тривалість послуги — від {MinDuration} до {MaxDuration} хвилин, кратно 5.");
        }

        var days = new List<WorkingDay>();
        foreach (var day in request.Days ?? [])
        {
            if (!Weekdays.IsValid(day.Weekday) || days.Any(d => d.Weekday == day.Weekday))
                return Bad("Кожен день тижня можна вказати лише раз.");
            if (!Times.TryParseTime(day.Start, out var start) || !Times.TryParseTime(day.End, out var end))
                return Bad("Час роботи — у форматі ГГ:ХХ, напр. 09:00.");
            if (end <= start)
                return Bad($"{Weekdays.Short[day.Weekday - 1]}: кінець робочого дня має бути пізніше за початок.");
            if ((end - start).TotalMinutes < request.SlotStepMinutes)
                return Bad($"{Weekdays.Short[day.Weekday - 1]}: робочий день коротший за крок запису.");

            days.Add(new WorkingDay { MasterId = profile!.Id, Weekday = day.Weekday, Start = start, End = end });
        }

        var today = clock.Today;
        var daysOff = new SortedSet<DateOnly>();
        foreach (var value in request.DaysOff ?? [])
        {
            if (!Times.TryParseDate(value, out var date)) return Bad("Вихідна дата — у форматі РРРР-ММ-ДД.");
            if (date >= today) daysOff.Add(date); // минулі вихідні вже ні на що не впливають
        }
        if (daysOff.Count > MaxDaysOff) return Bad($"Вихідних дат — до {MaxDaysOff}.");

        if (request.IsPublished)
        {
            var missing = new List<string>();
            if (city.Length == 0) missing.Add("місто");
            if (address.Length == 0) missing.Add("адресу");
            if (services.Count == 0) missing.Add("хоча б одну послугу");
            if (days.Count == 0) missing.Add("хоча б один робочий день");
            if (missing.Count > 0) return Bad($"Щоб опублікувати профіль, додайте {string.Join(", ", missing)}.");

            var longest = services.Max(s => s.DurationMinutes);
            if (days.All(d => (d.End - d.Start).TotalMinutes < longest))
                return Bad("Найдовша послуга не вміщується в жоден робочий день — збільште години або зменште тривалість.");
        }

        // ── Збереження: профіль + повна заміна послуг, розкладу і майбутніх вихідних ──
        profile!.DisplayName = name;
        profile.Description = description;
        profile.City = city;
        profile.Address = address;
        profile.Phone = phone;
        profile.ExperienceYears = request.ExperienceYears;
        profile.SlotStepMinutes = request.SlotStepMinutes;
        profile.BookingHorizonDays = request.BookingHorizonDays;
        profile.IsPublished = request.IsPublished;
        profile.UpdatedAt = DateTime.UtcNow;

        db.MasterServices.RemoveRange(await db.MasterServices.Where(s => s.MasterId == profile.Id).ToListAsync(ct));
        db.MasterServices.AddRange(services.Select(s => new MasterService
        {
            MasterId = profile.Id,
            ServiceTypeId = s.ServiceId,
            Price = s.Price,
            DurationMinutes = s.DurationMinutes
        }));

        db.WorkingDays.RemoveRange(await db.WorkingDays.Where(d => d.MasterId == profile.Id).ToListAsync(ct));
        db.WorkingDays.AddRange(days);

        db.DaysOff.RemoveRange(await db.DaysOff.Where(d => d.MasterId == profile.Id && d.Date >= today).ToListAsync(ct));
        db.DaysOff.AddRange(daysOff.Select(date => new DayOff { MasterId = profile.Id, Date = date }));

        await db.SaveChangesAsync(ct);

        return Results.Ok(await MasterProfileView.BuildAsync(db, profile, today, ct));
    }

    private static IResult Bad(string message) => ApiResults.Error(StatusCodes.Status400BadRequest, message);
}
