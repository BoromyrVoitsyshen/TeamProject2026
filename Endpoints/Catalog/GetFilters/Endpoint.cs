using AiChat.Api.Catalog;

namespace AiChat.Api.Endpoints.Catalog.GetFilters;

/// <summary>
/// Дані для панелі фільтрів: увесь перелік послуг (з кількістю майстрів) і міста, де є майстри.
/// Перелік послуг потрібен і кабінету майстра — там він обирає, що робить.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(MasterCatalog catalog, CancellationToken ct)
    {
        var types = await catalog.LoadServiceTypesAsync(ct);
        var masters = await catalog.LoadListedAsync(ct);

        var services = types.Select(t => new
        {
            id = t.Id,
            name = t.Name,
            category = t.Category,
            masters = masters.Count(m => m.Services.Any(s => s.ServiceId == t.Id))
        });

        var cities = masters
            .GroupBy(m => m.City.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { name = g.First().City.Trim(), masters = g.Count() })
            .OrderByDescending(c => c.masters)
            .ThenBy(c => c.name, StringComparer.CurrentCulture);

        var prices = masters.SelectMany(m => m.Services).Select(s => s.Price).ToList();

        return Results.Ok(new
        {
            services,
            cities,
            priceRange = prices.Count == 0 ? null : new { min = prices.Min(), max = prices.Max() }
        });
    }
}
