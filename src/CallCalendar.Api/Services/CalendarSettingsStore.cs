using CallCalendar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CallCalendar.Api.Services;

/// <summary>
/// Доступ к редактируемым настройкам календаря владельца: единственная строка.
/// Если строки ещё нет, она создаётся со значением окна записи по умолчанию из конфигурации.
/// </summary>
public sealed class CalendarSettingsStore(AppDbContext db, CalendarOptions options)
{
    /// <summary>Фиксированный идентификатор единственной строки настроек.</summary>
    public const int SettingsRowId = 1;

    public async Task<Data.Entities.CalendarSettings> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await db.CalendarSettings
            .FirstOrDefaultAsync(row => row.Id == SettingsRowId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new Data.Entities.CalendarSettings
        {
            Id = SettingsRowId,
            BookingWindowDays = options.DefaultBookingWindowDays,
        };

        db.CalendarSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task<int> GetBookingWindowDaysAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).BookingWindowDays;

    /// <summary>Обеденный перерыв по времени владельца или null, если он не задан.</summary>
    public async Task<(TimeOnly Start, TimeOnly End)?> GetLunchBreakAsync(CancellationToken cancellationToken)
    {
        var settings = await GetAsync(cancellationToken);

        if (settings.LunchBreakStart is not { } start || settings.LunchBreakEnd is not { } end)
        {
            return null;
        }

        return (start, end);
    }
}
