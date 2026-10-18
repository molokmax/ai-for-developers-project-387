using System.Globalization;

namespace CallCalendar.Api.Services;

/// <summary>
/// Настройки календаря владельца: часовой пояс для сетки слотов и значение окна записи
/// по умолчанию (им сидируется редактируемая настройка <c>Calendar:BookingWindowDays</c>).
/// </summary>
public sealed class CalendarOptions(TimeZoneInfo timeZone, int defaultBookingWindowDays)
{
    public const string ConfigurationKey = "Calendar:TimeZone";

    public const string BookingWindowConfigurationKey = "Calendar:BookingWindowDays";

    public const string DefaultTimeZoneId = "Europe/Moscow";

    public const int FallbackBookingWindowDays = 14;

    public TimeZoneInfo TimeZone { get; } = timeZone;

    /// <summary>Окно записи по умолчанию из конфигурации: используется при первом сидировании.</summary>
    public int DefaultBookingWindowDays { get; } = defaultBookingWindowDays;

    public static CalendarOptions FromConfiguration(IConfiguration configuration)
    {
        var timeZoneId = configuration[ConfigurationKey] ?? DefaultTimeZoneId;
        return new CalendarOptions(
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId),
            ReadBookingWindowDays(configuration));
    }

    private static int ReadBookingWindowDays(IConfiguration configuration)
    {
        var raw = configuration[BookingWindowConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FallbackBookingWindowDays;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days < 1)
        {
            throw new InvalidOperationException(
                $"Некорректное значение {BookingWindowConfigurationKey}='{raw}': ожидается целое число >= 1");
        }

        return days;
    }
}
