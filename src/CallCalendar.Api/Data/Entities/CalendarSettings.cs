namespace CallCalendar.Api.Data.Entities;

/// <summary>
/// Настройки календаря владельца. Единственная строка с фиксированным Id:
/// редактируются владельцем через API, при отсутствии создаются из конфигурации.
/// </summary>
public class CalendarSettings
{
    /// <summary>Идентификатор единственной строки настроек.</summary>
    public int Id { get; set; }

    /// <summary>Окно записи в календарных днях, включая текущий.</summary>
    public int BookingWindowDays { get; set; }
}
