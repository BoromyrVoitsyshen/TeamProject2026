using AiChat.Api.Accounts;
using AiChat.Api.Data;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Endpoints.MasterCabinet;

/// <summary>Профіль майстра для кабінету: усі поля, які він може змінити.</summary>
public static class MasterProfileView
{
    /// <summary>Профіль поточного майстра або готова помилка 401/403/404.</summary>
    public static async Task<(MasterProfile? Profile, IResult? Error)> LoadOwnAsync(
        HttpContext http, AppDbContext db, bool track, CancellationToken ct)
    {
        var (user, error) = await CurrentUser.RequireAsync(http, db, UserRole.Master, ct);
        if (error != null) return (null, error);

        var query = track ? db.Masters : db.Masters.AsNoTracking();
        var profile = await query.FirstOrDefaultAsync(m => m.UserId == user!.Id, ct);
        return profile == null
            ? (null, ApiResults.Error(StatusCodes.Status404NotFound, "Профіль майстра не знайдено."))
            : (profile, null);
    }

    public static async Task<object> BuildAsync(AppDbContext db, MasterProfile profile, DateOnly today, CancellationToken ct)
    {
        var services = await db.MasterServices.AsNoTracking()
            .Where(s => s.MasterId == profile.Id)
            .ToListAsync(ct);

        var days = await db.WorkingDays.AsNoTracking()
            .Where(d => d.MasterId == profile.Id)
            .OrderBy(d => d.Weekday)
            .ToListAsync(ct);

        var daysOff = await db.DaysOff.AsNoTracking()
            .Where(d => d.MasterId == profile.Id && d.Date >= today)
            .OrderBy(d => d.Date)
            .Select(d => d.Date)
            .ToListAsync(ct);

        return new
        {
            profile.Id,
            profile.DisplayName,
            profile.Description,
            profile.City,
            profile.Address,
            profile.Phone,
            profile.ExperienceYears,
            profile.SlotStepMinutes,
            profile.BookingHorizonDays,
            profile.IsPublished,
            services = services.Select(s => new { serviceId = s.ServiceTypeId, s.Price, s.DurationMinutes }),
            days = days.Select(d => new { d.Weekday, start = Times.Format(d.Start), end = Times.Format(d.End) }),
            daysOff = daysOff.Select(Times.Format)
        };
    }
}
