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
        LunchBreakStart = FormatTime(settings.LunchBreakStart),
        LunchBreakEnd = FormatTime(settings.LunchBreakEnd),
    };

    private static string? FormatTime(TimeOnly? value) =>
        value?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

public sealed class SettingsUpdateEndpoint(AppDbContext db, CalendarSettingsStore settings)
    : Bases.SettingsUpdateEndpointBase
{
    public override async Task<Results<Ok<Dtos.CalendarSettings>, BadRequest<Dtos.ProblemDetails>>> HandleAsync(
        Dtos.UpdateCalendarSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveLunchBreak(request.LunchBreakStart, request.LunchBreakEnd, out var lunchStart, out var lunchEnd, out var error))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(error));
        }

        var current = await settings.GetAsync(cancellationToken);
        current.BookingWindowDays = request.BookingWindowDays;
        current.LunchBreakStart = lunchStart;
        current.LunchBreakEnd = lunchEnd;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(SettingsGetEndpoint.ToDto(current));
    }

    /// <summary>
    /// Обеденный перерыв задаётся либо обеими границами, либо ни одной; начало строго раньше конца,
    /// обе границы - в пределах рабочего дня по времени владельца.
    /// </summary>
    private static bool TryResolveLunchBreak(
        string? rawStart,
        string? rawEnd,
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

        if (parsedStart < SlotGrid.WorkStart || parsedEnd > SlotGrid.WorkEnd)
        {
            error = $"Обеденный перерыв должен целиком помещаться в рабочий день {SlotGrid.WorkStart:hh\\:mm}-{SlotGrid.WorkEnd:hh\\:mm} по времени владельца";
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
