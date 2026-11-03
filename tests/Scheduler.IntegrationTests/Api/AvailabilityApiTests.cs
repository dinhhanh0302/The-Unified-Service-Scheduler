using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>FR-06 end to end (SPEC §7, AC-06.x). "Now" is frozen at Mon 2026-11-09 08:00 +07:00.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Api")]
public sealed class AvailabilityApiTests(SchedulerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiTestData _data = new(factory);

    private static DateTimeOffset Tue(int hour, int minute = 0) =>
        new(2026, 11, 10, hour, minute, 0, TimeSpan.FromHours(7));

    private Task<HttpResponseMessage> GetAsync(Guid dealershipId, string date, Guid? serviceTypeId = null) =>
        _client.GetAsync(
            $"/api/v1/availability?dealershipId={dealershipId}&serviceTypeId={serviceTypeId ?? ApiTestData.OilChange}&date={date}");

    private static async Task<string[]> StartTimesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body.ToString());
        return body.GetProperty("startTimes").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    [Fact]
    [Trait("AC", "AC-06.1")]
    public async Task Get_FreeTuesday_ReturnsEveryQuarterHourFromEightToSixteen()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await GetAsync(scenario.DealershipId, "2026-11-10");

        var startTimes = await StartTimesAsync(response);
        startTimes.Should().HaveCount(33);
        startTimes[0].Should().Be("2026-11-10T08:00:00+07:00");
        startTimes[^1].Should().Be("2026-11-10T16:00:00+07:00");
    }

    [Fact]
    [Trait("AC", "AC-06.1")]
    public async Task Get_Response_EchoesTheRequestedDate()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await GetAsync(scenario.DealershipId, "2026-11-10");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("date").GetString().Should().Be("2026-11-10");
    }

    [Fact]
    [Trait("AC", "AC-06.2")]
    public async Task Get_AllLiftBaysBookedTenToTwelve_ExcludesOverlappingStarts()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 2, technicians: 4);
        var vehicleA = await _data.CreateVehicleAsync(scenario.CustomerId);
        var vehicleB = await _data.CreateVehicleAsync(scenario.CustomerId);
        // Both Lift bays busy 10:00–12:00, using technicians 3 and 4; technicians 1 and 2 stay free.
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], scenario.TechnicianIds[2], vehicleA, Tue(10));
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], scenario.TechnicianIds[2], vehicleA, Tue(11));
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[1], scenario.TechnicianIds[3], vehicleB, Tue(10));
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[1], scenario.TechnicianIds[3], vehicleB, Tue(11));

        var startTimes = await StartTimesAsync(await GetAsync(scenario.DealershipId, "2026-11-10"));

        startTimes.Should().Contain(new[] { "2026-11-10T09:00:00+07:00", "2026-11-10T12:00:00+07:00" });
        startTimes.Select(t => DateTimeOffset.Parse(t, CultureInfo.InvariantCulture))
            .Should().NotContain(t => t > Tue(9) && t < Tue(12));
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task Get_Sunday_ReturnsEmptyList()
    {
        var scenario = await _data.CreateDealershipAsync();

        var startTimes = await StartTimesAsync(await GetAsync(scenario.DealershipId, "2026-11-15"));

        startTimes.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-06.4")]
    public async Task Get_Today_FirstStartIsOneHourFromNow()
    {
        var scenario = await _data.CreateDealershipAsync();

        var startTimes = await StartTimesAsync(await GetAsync(scenario.DealershipId, "2026-11-09"));

        startTimes[0].Should().Be("2026-11-09T09:00:00+07:00");
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task Get_UnknownDealership_Returns404()
    {
        var response = await GetAsync(Guid.NewGuid(), "2026-11-10");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("DEALERSHIP_NOT_FOUND");
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task Get_UnknownServiceType_Returns404()
    {
        var scenario = await _data.CreateDealershipAsync();

        var response = await GetAsync(scenario.DealershipId, "2026-11-10", serviceTypeId: Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("SERVICE_TYPE_NOT_FOUND");
    }

    [Theory]
    [InlineData("/api/v1/availability")]
    [InlineData("/api/v1/availability?dealershipId=d1000000-0000-0000-0000-000000000001&serviceTypeId=5e000000-0000-0000-0000-000000000001&date=not-a-date")]
    public async Task Get_MissingOrMalformedParameters_Returns400ValidationFailed(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("VALIDATION_FAILED");
    }
}
