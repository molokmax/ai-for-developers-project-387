using CallCalendar.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CallCalendar.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EventType> EventTypes => Set<EventType>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<CalendarSettings> CalendarSettings => Set<CalendarSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite хранит DateTime без информации о Kind; всё в UTC, Kind проставляется при материализации
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.Property(b => b.CustomerName).HasMaxLength(200);
            booking.Property(b => b.CustomerEmail).HasMaxLength(320);
            booking.Property(b => b.IdempotencyKey).HasMaxLength(100);

            booking.Property(b => b.StartUtc).HasConversion(utc);
            booking.Property(b => b.EndUtc).HasConversion(utc);
            booking.Property(b => b.CreatedUtc).HasConversion(utc);

            // Повтор с тем же Idempotency-Key возвращает первичный результат: ключ уникален глобально
            booking.HasIndex(b => b.IdempotencyKey).IsUnique();

            booking.HasIndex(b => b.StartUtc);
        });

        // Рабочий день добавлен позже остальных полей: для уже существующей строки настроек
        // БД-дефолт сохраняет прежнее поведение (09:00-18:00), владелец может изменить его через API.
        // TODO: захардкоженные 09:00/18:00 дублируют CalendarOptions.FallbackWorkStart/End;
        // при смене fallback БД-дефолт отстанет - сослаться на общие константы.
        modelBuilder.Entity<CalendarSettings>(settings =>
        {
            settings.Property(s => s.WorkStart).HasDefaultValue(new TimeOnly(9, 0));
            settings.Property(s => s.WorkEnd).HasDefaultValue(new TimeOnly(18, 0));
        });
    }
}
