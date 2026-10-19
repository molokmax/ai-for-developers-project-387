using System.Globalization;

namespace CallCalendar.Api.Services;

/// <summary>
/// Настройки календаря владельца: часовой пояс для сетки слотов и значения по умолчанию
/// редактируемых настроек (окно записи <c>Calendar:BookingWindowDays</c> и рабочий день
/// <c>Calendar:WorkStart</c>/<c>Calendar:WorkEnd</c>); ими сидируется единственная строка настроек.
/// </summary>
public sealed class CalendarOptions(
    TimeZoneInfo timeZone,
    int defaultBookingWindowDays,
    TimeOnly defaultWorkStart,
    TimeOnly defaultWorkEnd)
{
    public const string ConfigurationKey = "Calendar:TimeZone";

    public const string BookingWindowConfigurationKey = "Calendar:BookingWindowDays";

    public const string WorkStartConfigurationKey = "Calendar:WorkStart";

    public const string WorkEndConfigurationKey = "Calendar:WorkEnd";

    public const string DefaultTimeZoneId = "Europe/Moscow";

    public const int FallbackBookingWindowDays = 14;

    public static readonly TimeOnly FallbackWorkStart = new(9, 0);

    public static readonly TimeOnly FallbackWorkEnd = new(18, 0);

    public TimeZoneInfo TimeZone { get; } = timeZone;

    /// <summary>Окно записи по умолчанию из конфигурации: используется при первом сидировании.</summary>
    public int DefaultBookingWindowDays { get; } = defaultBookingWindowDays;

    /// <summary>Начало рабочего дня по умолчанию из конфигурации: используется при первом сидировании.</summary>
    public TimeOnly DefaultWorkStart { get; } = defaultWorkStart;

    /// <summary>Конец рабочего дня по умолчанию из конфигурации: используется при первом сидировании.</summary>
    public TimeOnly DefaultWorkEnd { get; } = defaultWorkEnd;

    public static CalendarOptions FromConfiguration(IConfiguration configuration)
    {
        var timeZoneId = configuration[ConfigurationKey] ?? DefaultTimeZoneId;
        var workStart = ReadTime(configuration, WorkStartConfigurationKey, FallbackWorkStart);
        var workEnd = ReadTime(configuration, WorkEndConfigurationKey, FallbackWorkEnd);

        if (workStart >= workEnd)
        {
            throw new InvalidOperationException(
                $"Некорректный рабочий день: {WorkStartConfigurationKey}='{workStart:HH\\:mm}' должен быть раньше {WorkEndConfigurationKey}='{workEnd:HH\\:mm}'");
        }

        if (!SlotGrid.IsAlignedToGrid(workStart))
        {
            throw new InvalidOperationException(
                $"Некорректный рабочий день: {WorkStartConfigurationKey}='{workStart:HH\\:mm}' должен быть кратен {SlotGrid.GridStepMinutes} минутам");
        }

        return new CalendarOptions(
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId),
            ReadBookingWindowDays(configuration),
            workStart,
            workEnd);
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

    private static TimeOnly ReadTime(IConfiguration configuration, string key, TimeOnly fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new InvalidOperationException(
                $"Некорректное значение {key}='{raw}': ожидается время в формате HH:mm");
        }

        return value;
    }
}
