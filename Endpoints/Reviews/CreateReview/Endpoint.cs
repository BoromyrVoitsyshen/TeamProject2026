using AiChat.Api.Accounts;
using AiChat.Api.Catalog;
using AiChat.Api.Persistence;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.Reviews.CreateReview;

public record CreateReviewRequest(int Rating, string? Text);

/// <summary>Відгук про майстра: 1–5 зірок і текст до 1000 символів (хто може — див. ReviewRules).</summary>
public class Endpoint : IEndpoint
{
    public const int MaxTextLength = 1000;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/masters/{id:int}/reviews", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        int id, CreateReviewRequest request, HttpContext http, AppDbContext db, BookingClock clock, CancellationToken ct)
    {
        var (client, error) = await CurrentUser.RequireAsync(http, db, UserRole.Client, ct);
        if (error != null) return error;

        if (!await db.Masters.AnyAsync(m => m.Id == id, ct))
            return ApiResults.Error(StatusCodes.Status404NotFound, "Майстра не знайдено.");

        if (request.Rating is < 1 or > 5)
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Оцініть від 1 до 5 зірок.");

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length > MaxTextLength)
            return ApiResults.Error(StatusCodes.Status400BadRequest, $"Відгук — до {MaxTextLength} символів.");

        var (canReview, hint) = await ReviewRules.AccessAsync(db, client, id, clock.Now, ct);
        if (!canReview)
            return ApiResults.Error(StatusCodes.Status409Conflict, hint ?? "Залишити відгук не можна.");

        var review = new Review
        {
            MasterId = id,
            ClientId = client!.Id,
            Rating = request.Rating,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Два відгуки одночасно (дві вкладки) — другий упреться в унікальний індекс
            return ApiResults.Error(StatusCodes.Status409Conflict, "Ви вже залишили відгук цьому майстрові.");
        }

        return Results.Json(new
        {
            review = new
            {
                review.Id,
                author = CurrentUser.PublicName(client.Name),
                review.Rating,
                review.Text,
                review.CreatedAt
            }
        }, statusCode: StatusCodes.Status201Created);
    }
}
