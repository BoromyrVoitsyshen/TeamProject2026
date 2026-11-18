using AiChat.Api.Accounts;
using AiChat.Api.Catalog;
using AiChat.Api.Data;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Catalog.GetMasterById;

/// <summary>
/// Повна інформація про майстра («Дізнатися більше»): опис, контакти, послуги, розклад, відгуки
/// і що може зробити той, хто дивиться (записатися, залишити відгук).
/// Неопублікований профіль бачить лише сам майстер — щоб перевірити, як він виглядатиме.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/masters/{id:int}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        int id, HttpContext http, AppDbContext db, MasterCatalog catalog, BookingClock clock, CancellationToken ct)
    {
        var found = await catalog.LoadOneAsync(id, ct);
        var viewer = await CurrentUser.LoadAsync(http, db, ct);

        if (found == null) return MasterNotFound();
        var (profile, card) = found.Value;

        var isOwner = viewer != null && viewer.Id == profile.UserId;
        if (!card.IsListed && !isOwner) return MasterNotFound();

        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.MasterId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var authorIds = reviews.Select(r => r.ClientId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var (canReview, reviewHint) = await ReviewRules.AccessAsync(db, viewer, id, clock.Now, ct);

        return Results.Ok(new
        {
            card.Id,
            card.Name,
            card.City,
            card.Address,
            card.ExperienceYears,
            card.Rating,
            card.ReviewCount,
            card.MinPrice,
            card.Services,
            card.Schedule,
            profile.Description,
            profile.Phone,
            slotStepMinutes = profile.SlotStepMinutes,
            bookingHorizonDays = profile.BookingHorizonDays,
            isListed = card.IsListed,
            ratingBreakdown = Enumerable.Range(1, 5).Reverse()
                .Select(stars => new { stars, count = reviews.Count(r => r.Rating == stars) }),
            reviews = reviews.Select(r => new
            {
                r.Id,
                author = CurrentUser.PublicName(authors.GetValueOrDefault(r.ClientId, "")),
                r.Rating,
                r.Text,
                r.CreatedAt
            }),
            viewer = new
            {
                role = viewer == null ? "guest" : viewer.Role == UserRole.Master ? "master" : "client",
                isOwner,
                canReview,
                reviewHint
            }
        });
    }

    private static IResult MasterNotFound() => ApiResults.Error(StatusCodes.Status404NotFound, "Майстра не знайдено.");
}
