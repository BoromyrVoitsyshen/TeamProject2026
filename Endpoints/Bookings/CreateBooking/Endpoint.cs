using AiChat.Api.Accounts;
using AiChat.Api.Catalog;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;

namespace AiChat.Api.Endpoints.Bookings.CreateBooking;

public record CreateBookingRequest(int MasterId, int ServiceId, string? Date, string? Time, string? Comment);

/// <summary>
/// Запис до майстра: { masterId, serviceId, date: "2026-11-12", time: "09:30", comment }.
/// Час має бути серед вільних у згенерованому графіку — інакше 409 (хтось щойно зайняв або розклад змінився).
/// </summary>
public class Endpoint : IEndpoint
{
    public const int MaxCommentLength = 500;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/bookings", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        CreateBookingRequest request,
        HttpContext http,
        AppDbContext db,
        MasterCatalog catalog,
        BookingClock clock,
        BookingLocks locks,
        CancellationToken ct)
    {
        var (client, error) = await CurrentUser.RequireAsync(http, db, UserRole.Client, ct);
        if (error != null) return error;

        if (!Times.TryParseDate(request.Date, out var date) || !Times.TryParseTime(request.Time, out var time))
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Оберіть дату й час запису.");

        var comment = (request.Comment ?? string.Empty).Trim();
        if (comment.Length > MaxCommentLength)
            return ApiResults.Error(StatusCodes.Status400BadRequest, $"Коментар — до {MaxCommentLength} символів.");

        var found = await catalog.LoadOneAsync(request.MasterId, ct);
        if (found == null || !found.Value.Card.IsListed)
            return ApiResults.Error(StatusCodes.Status404NotFound, "Майстра не знайдено.");
        var (profile, card) = found.Value;

        var offer = card.Services.FirstOrDefault(s => s.ServiceId == request.ServiceId);
        if (offer == null)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Цей майстер не надає такої послуги.");

        // Перевірка «час вільний» і запис — під замком майстра, щоб двоє не зайняли той самий час
        using (await locks.AcquireAsync(profile.Id, ct))
        {
            var now = clock.Now;
            var input = await ScheduleData.LoadAsync(db, profile, now, date, date, ct);
            if (!ScheduleGenerator.IsFree(input, offer.DurationMinutes, date, time))
                return ApiResults.Error(StatusCodes.Status409Conflict, "Цей час уже недоступний. Оберіть інший.");

            var startsAt = date.ToDateTime(time);
            var booking = new Booking
            {
                MasterId = profile.Id,
                ClientId = client!.Id,
                ServiceTypeId = offer.ServiceId,
                ServiceName = offer.Name,
                Price = offer.Price,
                StartsAt = startsAt,
                EndsAt = startsAt.AddMinutes(offer.DurationMinutes),
                Comment = comment.Length == 0 ? null : comment,
                Status = BookingStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);

            var view = await BookingViews.ForClientAsync(db, client.Id, [booking], now, ct);
            return Results.Json(new { booking = view[0] }, statusCode: StatusCodes.Status201Created);
        }
    }
}
