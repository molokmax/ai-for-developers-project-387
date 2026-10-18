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

    private static Dtos.CalendarSettings ToDto(Data.Entities.CalendarSettings settings) => new()
    {
        BookingWindowDays = settings.BookingWindowDays,
    };
}

public sealed class SettingsUpdateEndpoint(AppDbContext db, CalendarSettingsStore settings)
    : Bases.SettingsUpdateEndpointBase
{
    public override async Task<Results<Ok<Dtos.CalendarSettings>, BadRequest<Dtos.ProblemDetails>>> HandleAsync(
        Dtos.UpdateCalendarSettingsRequest request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        current.BookingWindowDays = request.BookingWindowDays;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new Dtos.CalendarSettings
        {
            BookingWindowDays = current.BookingWindowDays,
        });
    }
}
