namespace CallCalendar.Api.Services;

/// <summary>
/// Сетка слотов: каждые 30 минут в пределах рабочего дня владельца (настраиваемые начало и конец)
/// каждого дня по времени владельца, окно записи настраивается владельцем (число календарных дней, включая текущий).
/// Слот свободен, если его интервал не пересекается ни с одной существующей записью
/// и с обеденным перерывом владельца (если он задан).
/// Все времена в UTC (DateTime.Kind = Utc).
/// </summary>
public static class SlotGrid
{
    public const int GridStepMinutes = 30;

    /// <summary>Длительность допустима, если кратна 30 и минимум 30 минут.</summary>
    public static bool IsDurationValid(int minutes) => minutes >= GridStepMinutes && minutes % GridStepMinutes == 0;

    /// <summary>
    /// Свободные слоты для длительности <paramref name="duration"/> в окне записи
    /// <paramref name="bookingWindowDays"/> календарных дней, включая текущий:
    /// старта на сетке, интервал целиком в рабочем дне <paramref name="workHours"/>, не в прошлом,
    /// без пересечений с <paramref name="busy"/> и с обеденным перерывом <paramref name="lunchBreak"/>
    /// (границы по времени владельца, null - перерыв не задан).
    /// </summary>
    public static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> FreeSlots(
        TimeZoneInfo timeZone,
        TimeSpan duration,
        DateTime nowUtc,
        int bookingWindowDays,
        (TimeOnly Start, TimeOnly End) workHours,
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> busy,
        (TimeOnly Start, TimeOnly End)? lunchBreak)
    {
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date);

        for (var day = 0; day < bookingWindowDays; day++)
        {
            var date = todayLocal.AddDays(day);
            var workStartUtc = ToUtc(date.ToDateTime(workHours.Start), timeZone);
            var workEndUtc = ToUtc(date.ToDateTime(workHours.End), timeZone);
            var lunch = ResolveLunchBreak(date, timeZone, lunchBreak);

            for (var start = workStartUtc; start + duration <= workEndUtc; start += TimeSpan.FromMinutes(GridStepMinutes))
            {
                if (start < nowUtc)
                {
                    continue;
                }

                var end = start + duration;
                if (busy.Any(slot => Overlaps(start, end, slot.StartUtc, slot.EndUtc)))
                {
                    continue;
                }

                if (lunch is { } interval && Overlaps(start, end, interval.StartUtc, interval.EndUtc))
                {
                    continue;
                }

                yield return (start, end);
            }
        }
    }

    /// <summary>
    /// Проверка, что запрошенное начало записи лежит на сетке слотов: кратность 30 минутам,
    /// интервал целиком в рабочем дне <paramref name="workHours"/>, внутри окна записи
    /// <paramref name="bookingWindowDays"/> дней, не пересекается с обеденным перерывом
    /// <paramref name="lunchBreak"/> и время не прошло.
    /// </summary>
    public static bool IsStartAvailable(
        TimeZoneInfo timeZone,
        DateTime startUtc,
        TimeSpan duration,
        DateTime nowUtc,
        int bookingWindowDays,
        (TimeOnly Start, TimeOnly End) workHours,
        (TimeOnly Start, TimeOnly End)? lunchBreak,
        out string error)
    {
        var local = TimeZoneInfo.ConvertTime(startUtc, timeZone);

        if (local.Minute % GridStepMinutes != 0 || local.Second != 0 || local.Millisecond != 0)
        {
            error = "Начало записи должно лежать на сетке слотов с шагом 30 минут по времени владельца";
            return false;
        }

        if (local.TimeOfDay < workHours.Start.ToTimeSpan() || local.TimeOfDay + duration > workHours.End.ToTimeSpan())
        {
            error = $"Интервал записи должен целиком помещаться в рабочий день {workHours.Start:hh\\:mm}-{workHours.End:hh\\:mm} по времени владельца";
            return false;
        }

        var localDate = DateOnly.FromDateTime(local.Date);
        if (lunchBreak is { } breakBounds
            && ResolveLunchBreak(localDate, timeZone, lunchBreak) is { } lunch
            && Overlaps(startUtc, startUtc + duration, lunch.StartUtc, lunch.EndUtc))
        {
            error = $"Интервал записи не должен пересекаться с обеденным перерывом {breakBounds.Start:hh\\:mm}-{breakBounds.End:hh\\:mm} по времени владельца";
            return false;
        }

        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date);
        if (localDate < todayLocal || localDate > todayLocal.AddDays(bookingWindowDays - 1))
        {
            error = $"Запись доступна только в окне {bookingWindowDays} календарных дней по времени владельца, включая текущий день";
            return false;
        }

        if (startUtc < nowUtc)
        {
            error = "Прошедшее время недоступно, включая прошедшие слоты текущего дня";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static (DateTime StartUtc, DateTime EndUtc)? ResolveLunchBreak(
        DateOnly date,
        TimeZoneInfo timeZone,
        (TimeOnly Start, TimeOnly End)? lunchBreak)
    {
        if (lunchBreak is not { } lunch)
        {
            return null;
        }

        return (
            ToUtc(date.ToDateTime(lunch.Start), timeZone),
            ToUtc(date.ToDateTime(lunch.End), timeZone));
    }

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
}
