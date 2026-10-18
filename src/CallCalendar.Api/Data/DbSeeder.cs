using CallCalendar.Api.Data.Entities;
using CallCalendar.Api.Services;

namespace CallCalendar.Api.Data;

/// <summary>
/// Сидирует демонстрационные типы событий и настройки календаря. Слоты и записи не создаются:
/// свободные слоты вычисляются на лету.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db, int defaultBookingWindowDays)
    {
        var changed = false;

        if (!db.CalendarSettings.Any())
        {
            db.CalendarSettings.Add(new CalendarSettings
            {
                Id = CalendarSettingsStore.SettingsRowId,
                BookingWindowDays = defaultBookingWindowDays,
            });
            changed = true;
        }

        if (!db.EventTypes.Any())
        {
            db.EventTypes.AddRange(
                new EventType
                {
                    Name = "Вводный звонок",
                    Description = "Знакомство, обсуждение целей и запроса",
                    DurationMinutes = 30,
                },
                new EventType
                {
                    Name = "Обсуждение проекта",
                    Description = "Разбор требований, сроков и бюджета проекта",
                    DurationMinutes = 60,
                },
                new EventType
                {
                    Name = "Ревью кода",
                    Description = "Разбор архитектуры и кода проекта с рекомендациями",
                    DurationMinutes = 90,
                });
            changed = true;
        }

        if (changed)
        {
            db.SaveChanges();
        }
    }
}
