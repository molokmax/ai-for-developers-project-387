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

    /// <summary>Начало обеденного перерыва по времени владельца; null - перерыв не задан.</summary>
    public TimeOnly? LunchBreakStart { get; set; }

    /// <summary>Конец обеденного перерыва по времени владельца; null - перерыв не задан.</summary>
    public TimeOnly? LunchBreakEnd { get; set; }
}
