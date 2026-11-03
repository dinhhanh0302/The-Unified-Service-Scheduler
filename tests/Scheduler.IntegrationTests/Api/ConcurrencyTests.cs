using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>
/// BR-11 through the HTTP API: parallel requests for the same resources and window. Requests really run
/// concurrently against PostgreSQL; the exclusion constraints decide the winner.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Concurrency")]
public sealed class ConcurrencyTests(SchedulerApiFactory factory)
{
    private const string Url = "/api/v1/appointments";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiTestData _data = new(factory);

    private sealed record Outcome(HttpStatusCode Status, string? Code, Guid? BayId, Guid? TechnicianId);

    private async Task<Outcome[]> BookInParallelAsync(Scenario scenario, IEnumerable<Guid> vehicleIds)
    {
        var requests = vehicleIds.Select(async vehicleId =>
        {
            var response = await _client.PostAsJsonAsync(Url, new
            {
                customerId = scenario.CustomerId,
                vehicleId,
                dealershipId = scenario.DealershipId,
                serviceTypeId = ApiTestData.OilChange,
                startTime = "2026-11-10T09:00:00+07:00"
            });
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            return response.StatusCode == HttpStatusCode.Created
                ? new Outcome(response.StatusCode, null,
                    body.GetProperty("serviceBay").GetProperty("id").GetGuid(),
                    body.GetProperty("technician").GetProperty("id").GetGuid())
                : new Outcome(response.StatusCode,
                    body.TryGetProperty("code", out var code) ? code.GetString() : null, null, null);
        });

        return await Task.WhenAll(requests);
    }

    private async Task<List<Guid>> CreateVehiclesAsync(Scenario scenario, int count)
    {
        var vehicles = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            vehicles.Add(await _data.CreateVehicleAsync(scenario.CustomerId));
        }

        return vehicles;
    }

    [Fact]
    [Trait("AC", "AC-02.1")]
    public async Task TenParallelBookings_OneBayOneTechnician_ExactlyOneSucceeds()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 1);

        var outcomes = await BookInParallelAsync(scenario, await CreateVehiclesAsync(scenario, 10));

        outcomes.Should().NotContain(o => o.Status == HttpStatusCode.InternalServerError);
        outcomes.Count(o => o.Status == HttpStatusCode.Created).Should().Be(1);
        outcomes.Where(o => o.Status != HttpStatusCode.Created)
            .Should().HaveCount(9)
            .And.OnlyContain(o => o.Status == HttpStatusCode.Conflict && o.Code == "SLOT_UNAVAILABLE");
        (await _data.CountConfirmedAsync(scenario.DealershipId)).Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-02.2")]
    public async Task TenParallelBookings_TwoBaysTwoTechnicians_ExactlyTwoSucceedWithoutDoubleBooking()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 2, technicians: 2);

        var outcomes = await BookInParallelAsync(scenario, await CreateVehiclesAsync(scenario, 10));

        outcomes.Should().NotContain(o => o.Status == HttpStatusCode.InternalServerError);
        var created = outcomes.Where(o => o.Status == HttpStatusCode.Created).ToArray();
        created.Should().HaveCount(2);
        created.Select(o => o.BayId).Should().OnlyHaveUniqueItems();
        created.Select(o => o.TechnicianId).Should().OnlyHaveUniqueItems();
        outcomes.Where(o => o.Status != HttpStatusCode.Created)
            .Should().OnlyContain(o => o.Status == HttpStatusCode.Conflict && o.Code == "SLOT_UNAVAILABLE");
        (await _data.CountConfirmedAsync(scenario.DealershipId)).Should().Be(2);
    }

    [Fact]
    [Trait("AC", "AC-02.3")]
    public async Task FiveParallelBookings_SameVehicle_ExactlyOneSucceeds()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 5, technicians: 5);

        var outcomes = await BookInParallelAsync(scenario, Enumerable.Repeat(scenario.VehicleId, 5));

        outcomes.Should().NotContain(o => o.Status == HttpStatusCode.InternalServerError);
        outcomes.Count(o => o.Status == HttpStatusCode.Created).Should().Be(1);
        outcomes.Where(o => o.Status != HttpStatusCode.Created)
            .Should().HaveCount(4)
            .And.OnlyContain(o => o.Status == HttpStatusCode.Conflict && o.Code == "VEHICLE_ALREADY_BOOKED");
        (await _data.CountConfirmedAsync(scenario.DealershipId)).Should().Be(1);
    }
}
