using AiChat.Api.Data;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Catalog;

public record ServiceOffer(int ServiceId, string Name, string Category, int Price, int DurationMinutes);

public record WorkHours(int Weekday, string Day, string Start, string End);

/// <summary>Майстер у каталозі: усе, що потрібно для картки і для фільтрів.</summary>
public record MasterCard(
    int Id,
    string Name,
    string City,
    string Address,
    int ExperienceYears,
    double? Rating,
    int ReviewCount,
    int MinPrice,
    IReadOnlyList<ServiceOffer> Services,
    IReadOnlyList<WorkHours> Schedule,
    bool IsListed);

/// <summary>
/// Збирає картки майстрів із кількох простих запитів до бази. Фільтри й сортування каталогу
/// виконуються вже над цими картками в пам'яті (див. GetMasters): майстрів десятки-сотні, а так
/// пошук без урахування регістру працює і для кирилиці, чого SQLite сам не вміє.
/// </summary>
public class MasterCatalog(AppDbContext db)
{
    /// <summary>Усі майстри, яких видно в каталозі (опубліковані, з послугами й розкладом).</summary>
    public async Task<List<MasterCard>> LoadListedAsync(CancellationToken ct)
    {
        var masters = await db.Masters.AsNoTracking().Where(m => m.IsPublished).ToListAsync(ct);
        var cards = await BuildAsync(masters, ct);
        return cards.Where(c => c.IsListed).ToList();
    }

    public async Task<(MasterProfile Profile, MasterCard Card)?> LoadOneAsync(int id, CancellationToken ct)
    {
        var master = await db.Masters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (master == null) return null;

        var cards = await BuildAsync([master], ct);
        return (master, cards[0]);
    }

    public Task<List<ServiceType>> LoadServiceTypesAsync(CancellationToken ct) =>
        db.ServiceTypes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(ct);

    private async Task<List<MasterCard>> BuildAsync(List<MasterProfile> masters, CancellationToken ct)
    {
        var ids = masters.Select(m => m.Id).ToList();

        var types = (await LoadServiceTypesAsync(ct)).ToDictionary(t => t.Id);

        var offers = await db.MasterServices.AsNoTracking()
            .Where(s => ids.Contains(s.MasterId))
            .ToListAsync(ct);

        var days = await db.WorkingDays.AsNoTracking()
            .Where(d => ids.Contains(d.MasterId))
            .ToListAsync(ct);

        var stats = await db.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.MasterId))
            .GroupBy(r => r.MasterId)
            .Select(g => new { MasterId = g.Key, Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .ToListAsync(ct);

        return masters.Select(master =>
        {
            var services = offers
                .Where(o => o.MasterId == master.Id && types.ContainsKey(o.ServiceTypeId))
                .Select(o =>
                {
                    var type = types[o.ServiceTypeId];
                    return new ServiceOffer(o.ServiceTypeId, type.Name, type.Category, o.Price, o.DurationMinutes);
                })
                .OrderBy(o => types[o.ServiceId].SortOrder)
                .ToList();

            var schedule = days
                .Where(d => d.MasterId == master.Id)
                .OrderBy(d => d.Weekday)
                .Select(d => new WorkHours(d.Weekday, Weekdays.Short[d.Weekday - 1], Times.Format(d.Start), Times.Format(d.End)))
                .ToList();

            var stat = stats.FirstOrDefault(s => s.MasterId == master.Id);

            return new MasterCard(
                master.Id,
                master.DisplayName,
                master.City,
                master.Address,
                master.ExperienceYears,
                stat == null ? null : Math.Round(stat.Average, 1, MidpointRounding.AwayFromZero),
                stat?.Count ?? 0,
                services.Count == 0 ? 0 : services.Min(s => s.Price),
                services,
                schedule,
                IsListed: master.IsPublished && services.Count > 0 && schedule.Count > 0);
        }).ToList();
    }
}
