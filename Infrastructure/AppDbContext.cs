using AiChat.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Infrastructure;

/// <summary>
/// База даних застосунку (SQLite, файл data/app.db). Поки що в ній лише таблиці ASP.NET Identity
/// (користувачі, ролі, логіни). Чати лишаються в JSON-файлах — їх перенесемо сюди окремим кроком.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
}
