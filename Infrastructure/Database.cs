using AiChat.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Infrastructure;

/// <summary>Підключення бази (EF Core + SQLite) і сховища користувачів Identity.</summary>
public static class Database
{
    public static IdentityBuilder AddAppDatabase(this IdentityBuilder identity, WebApplicationBuilder builder)
    {
        // Рядок підключення можна перевизначити (ConnectionStrings:Default), інакше — файл у папці даних,
        // поруч із чатами: у Docker усе живе в одному volume
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? $"Data Source={Path.Combine(DataPaths.GetDataDirectory(builder.Configuration, builder.Environment), "app.db")}";

        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        return identity.AddEntityFrameworkStores<AppDbContext>();
    }

    /// <summary>
    /// Створює файл бази і таблиці, якщо їх ще немає. EnsureCreated простіший за міграції, але не вміє
    /// змінювати вже створену схему: коли додаватимемо власні таблиці (наприклад, чати), перейдемо на
    /// міграції EF Core (dotnet ef migrations add ...).
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
