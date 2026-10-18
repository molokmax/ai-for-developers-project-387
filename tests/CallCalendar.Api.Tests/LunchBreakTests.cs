using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CallCalendar.Api.Tests;

/// <summary>
/// Проверки обеденного перерыва владельца: перерыв задаётся настройками, исключается из выдачи
/// свободных слотов и запрещает создание записи в свои границы. Отдельный класс с собственной
/// фабрикой, чтобы изменение настроек не влияло на SmokeTests и SettingsTests.
/// </summary>
public class LunchBreakTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    // Часовой пояс владельца: Calendar:TimeZone из appsettings.json
    private static readonly TimeZoneInfo OwnerTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    private static readonly TimeOnly LunchStart = new(13, 0);
    private static readonly TimeOnly LunchEnd = new(14, 0);

    [Fact]
    public async Task Settings_DefaultLunchBreakIsNull()
    {
        var client = factory.CreateClient();

        var settings = JsonDocument.Parse(await client.GetStringAsync("/api/settings")).RootElement;

        Assert.Equal(JsonValueKind.Null, settings.GetProperty("lunchBreakStart").ValueKind);
        Assert.Equal(JsonValueKind.Null, settings.GetProperty("lunchBreakEnd").ValueKind);
    }

    [Fact]
    public async Task Settings_UpdateLunchBreak_RoundTripsAsPlainTime()
    {
        var client = factory.CreateClient();

        var response = await SetLunchAsync(client, LunchStart, LunchEnd);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("13:00:00", body.GetProperty("lunchBreakStart").GetString());
        Assert.Equal("14:00:00", body.GetProperty("lunchBreakEnd").GetString());

        // Перерыв можно снять, отправив null
        var cleared = await client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            lunchBreakStart = (string?)null,
            lunchBreakEnd = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var clearedBody = JsonDocument.Parse(await cleared.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, clearedBody.GetProperty("lunchBreakStart").ValueKind);
    }

    [Fact]
    public async Task Slots_ExcludeLunchBreakInterval()
    {
        var client = factory.CreateClient();

        var update = await SetLunchAsync(client, LunchStart, LunchEnd);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        try
        {
            var (eventTypeId, _) = await GetFirstEventTypeAsync(client);
            var slots = await GetSlotsAsync(client, eventTypeId);

            Assert.NotEmpty(slots);
            foreach (var slot in slots)
            {
                var start = TimeZoneInfo.ConvertTime(
                    DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture),
                    OwnerTimeZone).TimeOfDay;
                var end = TimeZoneInfo.ConvertTime(
                    DateTimeOffset.Parse(slot.GetProperty("endUtc").GetString()!, CultureInfo.InvariantCulture),
                    OwnerTimeZone).TimeOfDay;

                Assert.False(
                    start < LunchEnd.ToTimeSpan() && end > LunchStart.ToTimeSpan(),
                    $"Слот {start}-{end} пересекается с обеденным перерывом");
            }
        }
        finally
        {
            await ClearLunchAsync(client);
        }
    }

    [Fact]
    public async Task Booking_DuringLunchBreak_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var update = await SetLunchAsync(client, LunchStart, LunchEnd);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        try
        {
            var (eventTypeId, _) = await GetFirstEventTypeAsync(client);

            var response = await PostBookingAsync(
                client, Guid.NewGuid().ToString(), eventTypeId, LocalStartUtc(1, LunchStart));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Contains("обеден", problem.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await ClearLunchAsync(client);
        }
    }

    [Fact]
    public async Task Booking_RightAfterLunchBreak_IsAllowed()
    {
        var client = factory.CreateClient();

        var update = await SetLunchAsync(client, LunchStart, LunchEnd);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        try
        {
            var (eventTypeId, _) = await GetFirstEventTypeAsync(client);

            var response = await PostBookingAsync(
                client, Guid.NewGuid().ToString(), eventTypeId, LocalStartUtc(1, LunchEnd));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally
        {
            await ClearLunchAsync(client);
        }
    }

    [Theory]
    [InlineData("13:00", null)]
    [InlineData(null, "14:00")]
    [InlineData("14:00", "13:00")]
    [InlineData("08:00", "09:00")]
    [InlineData("17:00", "19:00")]
    public async Task Settings_InvalidLunchBreak_ReturnsBadRequest(string? start, string? end)
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            lunchBreakStart = start,
            lunchBreakEnd = end,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    private static Task<HttpResponseMessage> SetLunchAsync(HttpClient client, TimeOnly start, TimeOnly end) =>
        client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            lunchBreakStart = start.ToString("HH:mm", CultureInfo.InvariantCulture),
            lunchBreakEnd = end.ToString("HH:mm", CultureInfo.InvariantCulture),
        });

    private static Task<HttpResponseMessage> ClearLunchAsync(HttpClient client) =>
        client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            lunchBreakStart = (string?)null,
            lunchBreakEnd = (string?)null,
        });

    private static async Task<(long EventTypeId, int DurationMinutes)> GetFirstEventTypeAsync(HttpClient client)
    {
        var document = JsonDocument.Parse(await client.GetStringAsync("/api/event-types"));
        var first = document.RootElement.EnumerateArray().First();

        return (first.GetProperty("id").GetInt64(), first.GetProperty("durationMinutes").GetInt32());
    }

    private static async Task<JsonElement[]> GetSlotsAsync(HttpClient client, long eventTypeId)
    {
        var document = JsonDocument.Parse(await client.GetStringAsync($"/api/slots?eventTypeId={eventTypeId}"));

        return document.RootElement.EnumerateArray().ToArray();
    }

    private static string LocalStartUtc(int dayOffset, TimeOnly time)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, OwnerTimeZone).Date
            .AddDays(dayOffset)
            .Add(time.ToTimeSpan());

        return TimeZoneInfo.ConvertTimeToUtc(local, OwnerTimeZone)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client,
        string idempotencyKey,
        long eventTypeId,
        string startUtc)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            eventTypeId,
            startUtc,
            customerName = "Мария Иванова",
            customerEmail = "maria@example.com",
        });

        return await client.SendAsync(request);
    }
}
