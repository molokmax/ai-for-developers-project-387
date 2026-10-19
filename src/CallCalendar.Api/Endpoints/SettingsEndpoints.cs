using System.Globalization;
using CallCalendar.Api.Data;
using CallCalendar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

// Генерированный контракт из contracts/generated/openapi.json (MinimalOpenAPI)
using Bases = CallCalendar.Api.Contracts.Endpoints;
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

public sealed class SettingsGetEndpoint(CalendarSettingsStore settings)
    : Bases.SettingsGetEndpointBase
{
    public override async Task<Ok<Dtos.CalendarSettings>> HandleAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);

        return TypedResults.Ok(ToDto(current));
    }

    internal static Dtos.CalendarSettings ToDto(Data.Entities.CalendarSettings settings) => new()
    {
        BookingWindowDays = settings.BookingWindowDays,
        WorkStart = FormatTime(settings.WorkStart),
        WorkEnd = FormatTime(settings.WorkEnd),
        LunchBreakStart = FormatTime(settings.LunchBreakStart),
        LunchBreakEnd = FormatTime(settings.LunchBreakEnd)
    };

    private static string FormatTime(TimeOnly value) =>
        value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    // TODO: две перегрузки FormatTime различаются только nullability; можно оставить одну FormatTime(TimeOnly?)
    private static string? FormatTime(TimeOnly? value) =>
        value?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

public sealed class SettingsUpdateEndpoint(AppDbContext db, CalendarSettingsStore settings)
    : Bases.SettingsUpdateEndpointBase
{
    public override async Task<Results<Ok<Dtos.CalendarSettings>, BadRequest<Dtos.ProblemDetails>>> HandleAsync(
        Dtos.UpdateCalendarSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveWorkHours(request.WorkStart, request.WorkEnd, out var workStart, out var workEnd, out var error))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(error));
        }

        if (!TryResolveLunchBreak(request.LunchBreakStart, request.LunchBreakEnd, workStart, workEnd, out var lunchStart, out var lunchEnd, out error))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(error));
        }

        var current = await settings.GetAsync(cancellationToken);
        current.BookingWindowDays = request.BookingWindowDays;
        current.WorkStart = workStart;
        current.WorkEnd = workEnd;
        current.LunchBreakStart = lunchStart;
        current.LunchBreakEnd = lunchEnd;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(SettingsGetEndpoint.ToDto(current));
    }

    /// <summary>
    /// Рабочий день задаётся обеими границами; начало строго раньше конца и выровнено на сетку слотов.
    /// </summary>
    private static bool TryResolveWorkHours(
        string? rawStart,
        string? rawEnd,
        out TimeOnly start,
        out TimeOnly end,
        out string error)
    {
        start = default;
        end = default;

        if (!TryParseTime(rawStart, out start) || !TryParseTime(rawEnd, out end))
        {
            error = "Границы рабочего дня должны быть временем в формате HH:mm по времени владельца";
            return false;
        }

        if (start >= end)
        {
            error = "Начало рабочего дня должно быть раньше его конца";
            return false;
        }

        if (!SlotGrid.IsAlignedToGrid(start))
        {
            error = $"Начало рабочего дня должно быть кратно {SlotGrid.GridStepMinutes} минутам (например, 09:00 или 09:30)";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Обеденный перерыв задаётся либо обеими границами, либо ни одной; начало строго раньше конца,
    /// обе границы - в пределах рабочего дня по времени владельца.
    /// </summary>
    private static bool TryResolveLunchBreak(
        string? rawStart,
        string? rawEnd,
        TimeOnly workStart,
        TimeOnly workEnd,
        out TimeOnly? start,
        out TimeOnly? end,
        out string error)
    {
        start = null;
        end = null;

        var hasStart = !string.IsNullOrWhiteSpace(rawStart);
        var hasEnd = !string.IsNullOrWhiteSpace(rawEnd);

        if (!hasStart && !hasEnd)
        {
            error = string.Empty;
            return true;
        }

        if (hasStart != hasEnd)
        {
            error = "Обеденный перерыв задаётся обеими границами: началом и концом";
            return false;
        }

        if (!TryParseTime(rawStart, out var parsedStart) || !TryParseTime(rawEnd, out var parsedEnd))
        {
            error = "Границы обеденного перерыва должны быть временем в формате HH:mm по времени владельца";
            return false;
        }

        if (parsedStart >= parsedEnd)
        {
            error = "Начало обеденного перерыва должно быть раньше его конца";
            return false;
        }

        if (parsedStart < workStart || parsedEnd > workEnd)
        {
            error = $"Обеденный перерыв должен целиком помещаться в рабочий день {workStart:hh\\:mm}-{workEnd:hh\\:mm} по времени владельца";
            return false;
        }

        start = parsedStart;
        end = parsedEnd;
        error = string.Empty;
        return true;
    }

    private static bool TryParseTime(string? raw, out TimeOnly value) =>
        TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
}
