using AiChat.Api.Accounts;
using AiChat.Api.Catalog;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;

namespace AiChat.Api.Endpoints.Catalog.GetMasterSlots;

/// <summary>
/// Вільний час для запису на послугу: GET /api/masters/5/slots?serviceId=2&amp;from=2026-11-12&amp;days=7.
/// Графік генерується з розкладу майстра (напр. Ср, Чт, Пт 09:00–18:00), його вихідних і вже зроблених записів.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/masters/{id:int}/slots", HandleAsync)
            .AllowAnonymous(); // публічне: доступне гостям (див. FallbackPolicy в Accounts/AuthSetup.cs)
    }

    private static async Task<IResult> HandleAsync(
        int id,
        HttpContext http,
        AppDbContext db,
        MasterCatalog catalog,
        BookingClock clock,
        CancellationToken ct,
        int? serviceId = null,
        string? from = null,
        int? days = null)
    {
        var found = await catalog.LoadOneAsync(id, ct);
        if (found == null) return ApiResults.Error(StatusCodes.Status404NotFound, "Майстра не знайдено.");
        var (profile, card) = found.Value;

        var viewer = http.User.GetUserId();
        if (!card.IsListed && viewer != profile.UserId)
            return ApiResults.Error(StatusCodes.Status404NotFound, "Майстра не знайдено.");

        var offer = card.Services.FirstOrDefault(s => s.ServiceId == serviceId);
        if (offer == null)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Оберіть послугу, яку надає цей майстер.");

        var now = clock.Now;
        var start = clock.Today;
        if (from != null)
        {
            if (!Times.TryParseDate(from, out start))
                return ApiResults.Error(StatusCodes.Status400BadRequest, "Дата має бути у форматі РРРР-ММ-ДД.");
            if (start < clock.Today) start = clock.Today;
        }

        var dayCount = Math.Clamp(days ?? profile.BookingHorizonDays, 1, ScheduleGenerator.MaxDaysPerRequest);
        var input = await ScheduleData.LoadAsync(db, profile, now, start, start.AddDays(dayCount), ct);
        var free = ScheduleGenerator.FreeSlots(input, offer.DurationMinutes, start, dayCount);

        return Results.Ok(new
        {
            serviceId = offer.ServiceId,
            durationMinutes = offer.DurationMinutes,
            price = offer.Price,
            lastBookableDay = Times.Format(ScheduleGenerator.LastBookableDay(now, profile.BookingHorizonDays)),
            days = free.Select(d => new
            {
                date = Times.Format(d.Date),
                weekday = Weekdays.Short[Weekdays.Of(d.Date) - 1],
                isWorking = d.IsWorking,
                slots = d.Slots.Select(Times.Format)
            })
        });
    }
}
