using System.Globalization;
using AiChat.Api.Catalog;

namespace AiChat.Api.Endpoints.Catalog.GetMasters;

/// <summary>
/// Каталог майстрів із фільтрами. Усі параметри необов'язкові:
///   service        — id послуги: лише майстри, що її надають; ціна і сортування за ціною — саме цієї послуги
///   city           — місто (без урахування регістру)
///   q              — пошук за адресою, містом або ім'ям
///   minPrice, maxPrice — ціна, грн: ціна обраної послуги, а без послуги — хоч одна послуга в цих межах
///   minExperience  — стаж від, років
///   minRating      — рейтинг від (майстри без відгуків не проходять)
///   sort           — rating (типово) | reviews | price | experience
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/masters", HandleAsync)
            .AllowAnonymous(); // публічне: доступне гостям (див. FallbackPolicy в Accounts/AuthSetup.cs)
    }

    private static async Task<IResult> HandleAsync(
        MasterCatalog catalog,
        CancellationToken ct,
        int? service = null,
        string? city = null,
        string? q = null,
        int? minPrice = null,
        int? maxPrice = null,
        int? minExperience = null,
        double? minRating = null,
        string? sort = null)
    {
        var masters = await catalog.LoadListedAsync(ct);
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions ignoreCase = CompareOptions.IgnoreCase;

        IEnumerable<MasterCard> found = masters;

        if (service != null)
            found = found.Where(m => m.Services.Any(s => s.ServiceId == service));

        if (!string.IsNullOrWhiteSpace(city))
            found = found.Where(m => compare.Compare(m.City.Trim(), city.Trim(), ignoreCase) == 0);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            found = found.Where(m =>
                compare.IndexOf(m.Address, text, ignoreCase) >= 0 ||
                compare.IndexOf(m.City, text, ignoreCase) >= 0 ||
                compare.IndexOf(m.Name, text, ignoreCase) >= 0);
        }

        if (minPrice != null || maxPrice != null)
        {
            var low = minPrice ?? 0;
            var high = maxPrice ?? int.MaxValue;
            found = found.Where(m => m.Services.Any(s =>
                (service == null || s.ServiceId == service) && s.Price >= low && s.Price <= high));
        }

        if (minExperience != null)
            found = found.Where(m => m.ExperienceYears >= minExperience);

        if (minRating != null)
            found = found.Where(m => m.Rating != null && m.Rating >= minRating);

        // Ціна, яку показуємо на картці: обраної послуги, інакше найменша («від …»)
        int PriceOf(MasterCard m) => service == null
            ? m.MinPrice
            : m.Services.First(s => s.ServiceId == service).Price;

        var list = found.ToList();
        list = sort switch
        {
            "reviews" => list.OrderByDescending(m => m.ReviewCount).ThenByDescending(m => m.Rating ?? 0).ToList(),
            "price" => list.OrderBy(PriceOf).ThenByDescending(m => m.Rating ?? 0).ToList(),
            "experience" => list.OrderByDescending(m => m.ExperienceYears).ThenByDescending(m => m.Rating ?? 0).ToList(),
            _ => list.OrderByDescending(m => m.Rating ?? 0).ThenByDescending(m => m.ReviewCount).ToList()
        };

        return Results.Ok(new
        {
            total = list.Count,
            masters = list.Select(m => new
            {
                m.Id,
                m.Name,
                m.City,
                m.Address,
                m.ExperienceYears,
                m.Rating,
                m.ReviewCount,
                price = PriceOf(m),
                priceIsFrom = service == null && m.Services.Count > 1,
                m.Services,
                m.Schedule
            })
        });
    }
}
