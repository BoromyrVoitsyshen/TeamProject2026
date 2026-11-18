using AiChat.Api.Accounts;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Bookings;

/// <summary>Як запис виглядає в API: для клієнта — з даними майстра, для майстра — з контактами клієнта.</summary>
public static class BookingViews
{
    /// <summary>Скасувати можна лише запис, час якого ще не настав.</summary>
    public static bool CanCancel(Booking booking, DateTime now) =>
        booking.Status == BookingStatus.Active && booking.StartsAt > now;

    public static string StateOf(Booking booking, DateTime now) => booking.Status switch
    {
        BookingStatus.Cancelled => "cancelled",
        _ when booking.EndsAt <= now => "done",
        _ => "upcoming"
    };

    public static async Task<List<object>> ForClientAsync(
        AppDbContext db, int clientId, List<Booking> bookings, DateTime now, CancellationToken ct)
    {
        var masterIds = bookings.Select(b => b.MasterId).Distinct().ToList();
        var masters = await db.Masters.AsNoTracking()
            .Where(m => masterIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var reviewed = (await db.Reviews.AsNoTracking()
            .Where(r => r.ClientId == clientId && masterIds.Contains(r.MasterId))
            .Select(r => r.MasterId)
            .ToListAsync(ct)).ToHashSet();

        return bookings.Select(b =>
        {
            var master = masters.GetValueOrDefault(b.MasterId);
            var state = StateOf(b, now);
            return (object)new
            {
                b.Id,
                startsAt = Times.Format(b.StartsAt),
                endsAt = Times.Format(b.EndsAt),
                service = b.ServiceName,
                b.Price,
                b.Comment,
                state,
                cancelledBy = b.CancelledBy,
                canCancel = CanCancel(b, now),
                canReview = b.Status == BookingStatus.Active && b.StartsAt <= now && !reviewed.Contains(b.MasterId),
                master = master == null ? null : new
                {
                    master.Id,
                    name = master.DisplayName,
                    master.City,
                    master.Address,
                    master.Phone
                }
            };
        }).ToList();
    }

    public static async Task<List<object>> ForMasterAsync(
        AppDbContext db, List<Booking> bookings, DateTime now, CancellationToken ct)
    {
        var clientIds = bookings.Select(b => b.ClientId).Distinct().ToList();
        var clients = await db.Users.AsNoTracking()
            .Where(u => clientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return bookings.Select(b =>
        {
            var client = clients.GetValueOrDefault(b.ClientId);
            return (object)new
            {
                b.Id,
                startsAt = Times.Format(b.StartsAt),
                endsAt = Times.Format(b.EndsAt),
                service = b.ServiceName,
                b.Price,
                b.Comment,
                state = StateOf(b, now),
                cancelledBy = b.CancelledBy,
                canCancel = CanCancel(b, now),
                client = client == null ? null : new
                {
                    name = client.Name,
                    client.Phone,
                    client.Email
                }
            };
        }).ToList();
    }

    /// <summary>Хто бачить запис: клієнт, який записався, або майстер, до якого записались.</summary>
    public static async Task<(bool IsClient, bool IsMaster)> AccessAsync(AppDbContext db, User user, Booking booking, CancellationToken ct)
    {
        if (user.Role == UserRole.Client) return (booking.ClientId == user.Id, false);

        var ownsMaster = await db.Masters.AnyAsync(m => m.Id == booking.MasterId && m.UserId == user.Id, ct);
        return (false, ownsMaster);
    }
}
