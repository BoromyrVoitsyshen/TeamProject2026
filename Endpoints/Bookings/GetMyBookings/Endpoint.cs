using AiChat.Api.Accounts;
using AiChat.Api.Data;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Bookings.GetMyBookings;

/// <summary>
/// Мої записи. Клієнт бачить, до кого записався; майстер — хто записався до нього (з контактами).
/// Свіжі зверху; сторінка сама ділить на «Найближчі» і «Минулі».
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/bookings/my", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct)
    {
        var user = await CurrentUser.LoadAsync(http, db, ct);
        if (user == null) return ApiResults.Error(StatusCodes.Status401Unauthorized, "Увійдіть в акаунт.");

        var now = clock.Now;

        if (user.Role == UserRole.Client)
        {
            var mine = await db.Bookings.AsNoTracking()
                .Where(b => b.ClientId == user.Id)
                .OrderByDescending(b => b.StartsAt)
                .ToListAsync(ct);

            return Results.Ok(new { role = "client", bookings = await BookingViews.ForClientAsync(db, user.Id, mine, now, ct) });
        }

        var masterId = await db.Masters.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);

        var toMe = masterId == null
            ? []
            : await db.Bookings.AsNoTracking()
                .Where(b => b.MasterId == masterId)
                .OrderByDescending(b => b.StartsAt)
                .ToListAsync(ct);

        return Results.Ok(new { role = "master", bookings = await BookingViews.ForMasterAsync(db, toMe, now, ct) });
    }
}
