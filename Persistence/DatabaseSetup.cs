using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Persistence;

public static class DatabaseSetup
{
    /// <summary>Файл бази поруч із розмовами: &lt;Storage:DataPath&gt;/app.db (типово data/app.db).</summary>
    public static string DatabasePath(IConfiguration configuration, IHostEnvironment environment)
    {
        var dataPath = configuration["Storage:DataPath"] ?? "data";
        var directory = Path.GetFullPath(dataPath, environment.ContentRootPath);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, configuration["Database:FileName"] ?? "app.db");
    }

    /// <summary>
    /// При старті: створює базу, якщо її ще немає, заповнює перелік послуг і, якщо
    /// Database:SeedDemoData = true, — демо-майстрів, клієнтів, записи й відгуки (лише в порожню базу).
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<BookingClock>();

        // EnsureCreated створює таблиці за моделлю, якщо файлу бази немає. Він НЕ оновлює вже створену базу:
        // змінили моделі в Persistence/Entities.cs — видаліть data/app.db (демо-дані створяться знову)
        // або перейдіть на міграції (dotnet ef migrations add …), див. README.
        await db.Database.EnsureCreatedAsync();

        if (!await db.ServiceTypes.AnyAsync())
        {
            db.ServiceTypes.AddRange(DemoData.ServiceCatalogue());
            await db.SaveChangesAsync();
        }

        if (app.Configuration.GetValue("Database:SeedDemoData", false) && !await db.Users.AnyAsync())
        {
            await DemoData.SeedAsync(db, clock.Today);
            app.Logger.LogInformation("Demo data added: masters, clients, bookings and reviews (password {Password})", DemoData.Password);
        }
    }
}
