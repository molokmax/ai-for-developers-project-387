using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CallCalendar.Api.Tests;

/// <summary>
/// Проверки редактируемых настроек календаря: окно записи по умолчанию берётся из конфигурации,
/// владелец может изменить его через API, и это влияет на выдачу слотов.
/// Отдельный класс с собственной фабрикой, чтобы изменение настроек не влияло на SmokeTests.
/// </summary>
public class SettingsTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    // Часовой пояс владельца: Calendar:TimeZone из appsettings.json
    private static readonly TimeZoneInfo OwnerTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    [Fact]
    public async Task Settings_DefaultWindowFromConfiguration_AndUpdateChangesSlotsWindow()
    {
        var client = factory.CreateClient();

        // Значение по умолчанию из appsettings.json (Calendar:BookingWindowDays)
        var initial = JsonDocument.Parse(await client.GetStringAsync("/api/settings")).RootElement;
        Assert.Equal(14, initial.GetProperty("bookingWindowDays").GetInt32());

        // Некорректное значение отклоняется валидацией контракта
        var invalid = await client.PutAsJsonAsync("/api/settings", new { bookingWindowDays = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);

        // Владелец уменьшает окно записи
        var updated = await client.PutAsJsonAsync("/api/settings", new { bookingWindowDays = 2 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, updatedBody.GetProperty("bookingWindowDays").GetInt32());

        // Слоты выдаются только в пределах нового окна: текущий и следующий день
        var eventTypes = JsonDocument.Parse(await client.GetStringAsync("/api/event-types")).RootElement;
        var eventTypeId = eventTypes.EnumerateArray().First().GetProperty("id").GetInt64();

        var slots = JsonDocument.Parse(await client.GetStringAsync($"/api/slots?eventTypeId={eventTypeId}")).RootElement;
        Assert.True(slots.GetArrayLength() > 0, "В двухдневном окне должны быть свободные слоты");

        var todayLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, OwnerTimeZone).Date;
        foreach (var slot in slots.EnumerateArray())
        {
            var local = TimeZoneInfo.ConvertTime(
                DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture),
                OwnerTimeZone).Date;

            Assert.InRange(local, todayLocal, todayLocal.AddDays(1));
        }

        // Возврат к значению по умолчанию, чтобы не влиять на прочие проверки
        var restored = await client.PutAsJsonAsync("/api/settings", new { bookingWindowDays = 14 });
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }
}
