using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CallCalendar.Api.Tests;

/// <summary>
/// Проверки настраиваемого рабочего дня владельца: границы сетки слотов задаются настройками,
/// слоты и записи ограничены рабочим днём, обеденный перерыв должен помещаться в него.
/// Отдельный класс с собственной фабрикой, чтобы изменение настроек не влияло на прочие тесты.
/// </summary>
public class WorkHoursTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    // Часовой пояс владельца: Calendar:TimeZone из appsettings.json
    private static readonly TimeZoneInfo OwnerTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    [Fact]
    public async Task Settings_DefaultWorkHours_AreNineToEighteen()
    {
        var client = factory.CreateClient();

        var settings = JsonDocument.Parse(await client.GetStringAsync("/api/settings")).RootElement;

        Assert.Equal("09:00:00", settings.GetProperty("workStart").GetString());
        Assert.Equal("18:00:00", settings.GetProperty("workEnd").GetString());
    }

    [Fact]
    public async Task Settings_UpdateWorkHours_RoundTripsAsPlainTime()
    {
        var client = factory.CreateClient();

        try
        {
            var response = await SetWorkHoursAsync(client, new TimeOnly(8, 0), new TimeOnly(20, 0));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("08:00:00", body.GetProperty("workStart").GetString());
            Assert.Equal("20:00:00", body.GetProperty("workEnd").GetString());
        }
        finally
        {
            await SetWorkHoursAsync(client, new TimeOnly(9, 0), new TimeOnly(18, 0));
        }
    }

    [Fact]
    public async Task Slots_RespectConfiguredWorkHours()
    {
        var client = factory.CreateClient();

        var update = await SetWorkHoursAsync(client, new TimeOnly(10, 0), new TimeOnly(12, 0));
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

                Assert.True(start >= TimeSpan.FromHours(10), $"Слот {start} начинается до рабочего дня");
                Assert.True(end <= TimeSpan.FromHours(12), $"Слот {start}-{end} выходит за рабочий день");
            }
        }
        finally
        {
            await SetWorkHoursAsync(client, new TimeOnly(9, 0), new TimeOnly(18, 0));
        }
    }

    [Fact]
    public async Task Booking_OutsideConfiguredWorkHours_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var update = await SetWorkHoursAsync(client, new TimeOnly(10, 0), new TimeOnly(12, 0));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        try
        {
            var (eventTypeId, _) = await GetFirstEventTypeAsync(client);

            // 09:00 на сетке, но раньше настроенного начала рабочего дня 10:00
            var response = await PostBookingAsync(
                client, Guid.NewGuid().ToString(), eventTypeId, LocalStartUtc(2, new TimeOnly(9, 0)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Contains("рабочий день", problem.GetProperty("detail").GetString());
        }
        finally
        {
            await SetWorkHoursAsync(client, new TimeOnly(9, 0), new TimeOnly(18, 0));
        }
    }

    [Fact]
    public async Task Settings_LunchOutsideWorkHours_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        try
        {
            var response = await client.PutAsJsonAsync("/api/settings", new
            {
                bookingWindowDays = 14,
                workStart = "10:00",
                workEnd = "12:00",
                lunchBreakStart = "09:00",
                lunchBreakEnd = "10:00"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Contains("рабочий день", problem.GetProperty("detail").GetString());
        }
        finally
        {
            await SetWorkHoursAsync(client, new TimeOnly(9, 0), new TimeOnly(18, 0));
        }
    }

    [Theory]
    [InlineData("18:00", "09:00")]
    [InlineData("09:00", "09:00")]
    public async Task Settings_InvalidWorkHours_ReturnsBadRequest(string start, string end)
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            workStart = start,
            workEnd = end
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    private static Task<HttpResponseMessage> SetWorkHoursAsync(HttpClient client, TimeOnly start, TimeOnly end) =>
        client.PutAsJsonAsync("/api/settings", new
        {
            bookingWindowDays = 14,
            workStart = start.ToString("HH:mm", CultureInfo.InvariantCulture),
            workEnd = end.ToString("HH:mm", CultureInfo.InvariantCulture),
            lunchBreakStart = (string?)null,
            lunchBreakEnd = (string?)null
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
            customerEmail = "maria@example.com"
        });

        return await client.SendAsync(request);
    }
}
