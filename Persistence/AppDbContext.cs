using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Persistence;

/// <summary>
/// База даних каталогу майстрів: акаунти, профілі, послуги, розклад, записи, відгуки.
/// Файл SQLite лежить у data/app.db (папка з Storage:DataPath), створюється сам при першому запуску.
/// Розмови з AI-помічником поки що живуть окремо — у JSON-файлах (ChatStorage).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MasterProfile> Masters => Set<MasterProfile>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<MasterService> MasterServices => Set<MasterService>();
    public DbSet<WorkingDay> WorkingDays => Set<WorkingDay>();
    public DbSet<DayOff> DaysOff => Set<DayOff>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(254);
            e.Property(u => u.Name).HasMaxLength(80);
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        });

        model.Entity<MasterProfile>(e =>
        {
            e.HasIndex(m => m.UserId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(m => m.DisplayName).HasMaxLength(80);
            e.Property(m => m.Description).HasMaxLength(2000);
            e.Property(m => m.City).HasMaxLength(60);
            e.Property(m => m.Address).HasMaxLength(200);
            e.Property(m => m.Phone).HasMaxLength(30);
        });

        model.Entity<ServiceType>(e =>
        {
            e.HasIndex(s => s.Name).IsUnique();
            e.Property(s => s.Name).HasMaxLength(100);
            e.Property(s => s.Category).HasMaxLength(60);
        });

        model.Entity<MasterService>(e =>
        {
            e.HasIndex(s => new { s.MasterId, s.ServiceTypeId }).IsUnique();
            e.HasOne<MasterProfile>().WithMany().HasForeignKey(s => s.MasterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ServiceType>().WithMany().HasForeignKey(s => s.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<WorkingDay>(e =>
        {
            e.HasIndex(d => new { d.MasterId, d.Weekday }).IsUnique();
            e.HasOne<MasterProfile>().WithMany().HasForeignKey(d => d.MasterId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<DayOff>(e =>
        {
            e.HasIndex(d => new { d.MasterId, d.Date }).IsUnique();
            e.HasOne<MasterProfile>().WithMany().HasForeignKey(d => d.MasterId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Booking>(e =>
        {
            e.HasIndex(b => new { b.MasterId, b.StartsAt });
            e.HasIndex(b => b.ClientId);
            e.HasOne<MasterProfile>().WithMany().HasForeignKey(b => b.MasterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(b => b.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ServiceType>().WithMany().HasForeignKey(b => b.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(b => b.ServiceName).HasMaxLength(100);
            e.Property(b => b.Comment).HasMaxLength(500);
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.CancelledBy).HasMaxLength(16);
        });

        model.Entity<Review>(e =>
        {
            e.HasIndex(r => new { r.MasterId, r.ClientId }).IsUnique();
            e.HasOne<MasterProfile>().WithMany().HasForeignKey(r => r.MasterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.Text).HasMaxLength(1000);
        });
    }
}
