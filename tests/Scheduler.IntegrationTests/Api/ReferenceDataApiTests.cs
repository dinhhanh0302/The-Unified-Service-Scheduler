using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scheduler.Infrastructure.Persistence;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>FR-09: reference data endpoints over the seed data.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Api")]
public sealed class ReferenceDataApiTests(SchedulerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonElement> GetJsonAsync(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.Should().Be(expected, body.ToString());
        return body;
    }

    [Fact]
    public async Task GetDealerships_IncludesSeededD1WithOpeningHours()
    {
        var body = await GetJsonAsync("/api/v1/dealerships");

        var d1 = body.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == SeedIds.Dealerships.D1);
        d1.GetProperty("timeZoneId").GetString().Should().Be("Asia/Ho_Chi_Minh");
        d1.GetProperty("openingTime").GetString().Should().StartWith("08:00");
        d1.GetProperty("closingTime").GetString().Should().StartWith("17:00");
        d1.GetProperty("openDays").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");
    }

    [Fact]
    public async Task GetServiceTypes_IncludesOilChangeWithRequirements()
    {
        var body = await GetJsonAsync("/api/v1/service-types");

        var oilChange = body.EnumerateArray().Single(s => s.GetProperty("code").GetString() == "OIL_CHANGE");
        oilChange.GetProperty("durationMinutes").GetInt32().Should().Be(60);
        oilChange.GetProperty("requiredQualification").GetString().Should().Be("General");
        oilChange.GetProperty("requiredBayType").GetString().Should().Be("Lift");
    }

    [Fact]
    public async Task GetCustomerVehicles_SeededCustomer_ReturnsOwnedVehicles()
    {
        var body = await GetJsonAsync($"/api/v1/customers/{SeedIds.Customers.An}/vehicles");

        body.EnumerateArray().Select(v => v.GetProperty("id").GetGuid())
            .Should().Contain(new[] { SeedIds.Vehicles.AnAccord, SeedIds.Vehicles.AnCivic })
            .And.NotContain(SeedIds.Vehicles.BinhModel3);
    }

    [Fact]
    public async Task GetCustomerVehicles_UnknownCustomer_Returns404()
    {
        var body = await GetJsonAsync($"/api/v1/customers/{Guid.NewGuid()}/vehicles", HttpStatusCode.NotFound);

        body.GetProperty("code").GetString().Should().Be("CUSTOMER_NOT_FOUND");
    }
}
