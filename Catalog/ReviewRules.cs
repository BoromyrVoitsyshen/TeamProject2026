using AiChat.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Catalog;

/// <summary>
/// Хто може залишити відгук: клієнт, який уже був у цього майстра (запис не скасовано і його час настав),
/// і лише один раз. Так рейтинг не накрутити.
/// </summary>
public static class ReviewRules
{
    public static async Task<(bool CanReview, string? Hint)> AccessAsync(
        AppDbContext db, User? viewer, int masterId, DateTime now, CancellationToken ct)
    {
        if (viewer == null) return (false, "Увійдіть як клієнт, щоб залишити відгук після візиту.");
        if (viewer.Role != UserRole.Client) return (false, null);

        if (await db.Reviews.AnyAsync(r => r.MasterId == masterId && r.ClientId == viewer.Id, ct))
            return (false, "Ви вже залишили відгук цьому майстрові.");

        var visited = await db.Bookings.AnyAsync(b =>
            b.MasterId == masterId &&
            b.ClientId == viewer.Id &&
            b.Status == BookingStatus.Active &&
            b.StartsAt <= now, ct);

        return visited ? (true, null) : (false, "Відгук можна залишити після візиту до майстра.");
    }
}
