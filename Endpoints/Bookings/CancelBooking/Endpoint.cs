using AiChat.Api.Accounts;
using AiChat.Api.Data;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Bookings.CancelBooking;

/// <summary>
/// Скасування запису клієнтом або майстром, доки час запису не настав. Запис не видаляється,
/// а позначається скасованим — час у графіку знову стає вільним.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/bookings/{id:int}/cancel", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(int id, HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(http, db, ct);
        if (user == null) return ApiResults.Error(StatusCodes.Status401Unauthorized, "Увійдіть в акаунт.");

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking == null) return NotFound();

        var (isClient, isMaster) = await BookingViews.AccessAsync(db, user, booking, ct);
        if (!isClient && !isMaster) return NotFound();

        var now = clock.Now;
        if (booking.Status == BookingStatus.Cancelled)
            return ApiResults.Error(StatusCodes.Status409Conflict, "Запис уже скасовано.");
        if (!BookingViews.CanCancel(booking, now))
            return ApiResults.Error(StatusCodes.Status409Conflict, "Час запису вже настав — скасувати не можна.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancelledBy = isClient ? "client" : "master";
        await db.SaveChangesAsync(ct);

        var view = isClient
            ? await BookingViews.ForClientAsync(db, user.Id, [booking], now, ct)
            : await BookingViews.ForMasterAsync(db, [booking], now, ct);

        return Results.Ok(new { booking = view[0] });
    }

    // Чужий запис — так само 404, щоб не підказувати, що запис із таким id існує
    private static IResult NotFound() => ApiResults.Error(StatusCodes.Status404NotFound, "Запис не знайдено.");
}
