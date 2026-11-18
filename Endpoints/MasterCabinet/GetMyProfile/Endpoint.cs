using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;

namespace AiChat.Api.Endpoints.MasterCabinet.GetMyProfile;

/// <summary>Профіль майстра для редагування в кабінеті.</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/my/master", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct)
    {
        var (profile, error) = await MasterProfileView.LoadOwnAsync(http, db, track: false, ct);
        if (error != null) return error;

        return Results.Ok(await MasterProfileView.BuildAsync(db, profile!, clock.Today, ct));
    }
}
