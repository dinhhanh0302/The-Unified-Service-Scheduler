using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>FR-07 / BR-12 end to end: cancelling releases resources (SPEC §7, AC-07.x).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Api")]
public sealed class LifecycleApiTests(SchedulerApiFactory factory)
{
    private const string Url = "/api/v1/appointments";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiTestData _data = new(factory);

    private Task<HttpResponseMessage> BookAsync(Scenario scenario, Guid vehicleId) =>
        _client.PostAsJsonAsync(Url, new
        {
            customerId = scenario.CustomerId,
            vehicleId,
            dealershipId = scenario.DealershipId,
            serviceTypeId = ApiTestData.OilChange,
            startTime = "2026-11-10T09:00:00+07:00"
        });

    private async Task<JsonElement> BookOkAsync(Scenario scenario, Guid vehicleId)
    {
        var response = await BookAsync(scenario, vehicleId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body.ToString());
        return body;
    }

    private Task<HttpResponseMessage> CancelAsync(Guid id) => _client.PostAsync($"{Url}/{id}/cancel", content: null);

    [Fact]
    [Trait("AC", "AC-07.1")]
    public async Task Cancel_FutureConfirmed_Returns200Cancelled()
    {
        var scenario = await _data.CreateDealershipAsync();
        var booked = await BookOkAsync(scenario, scenario.VehicleId);

        var response = await CancelAsync(booked.GetProperty("id").GetGuid());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body.ToString());
        body.GetProperty("status").GetString().Should().Be("Cancelled");
        body.GetProperty("cancelledAtUtc").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    [Trait("AC", "AC-07.2")]
    public async Task Cancel_ThenBookSameWindow_SameBayAndTechnicianAreFreeAgain()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 1);
        var first = await BookOkAsync(scenario, scenario.VehicleId);
        (await CancelAsync(first.GetProperty("id").GetGuid())).StatusCode.Should().Be(HttpStatusCode.OK);
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);

        var second = await BookOkAsync(scenario, otherVehicle);

        second.GetProperty("serviceBay").GetProperty("id").GetGuid().Should().Be(scenario.BayIds[0]);
        second.GetProperty("technician").GetProperty("id").GetGuid().Should().Be(scenario.TechnicianIds[0]);
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public async Task Cancel_AlreadyCancelled_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync();
        var id = (await BookOkAsync(scenario, scenario.VehicleId)).GetProperty("id").GetGuid();
        await CancelAsync(id);

        var response = await CancelAsync(id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("APPOINTMENT_NOT_CANCELLABLE");
    }

    [Fact]
    [Trait("AC", "AC-07.4")]
    public async Task Cancel_StartInThePast_Returns409()
    {
        var scenario = await _data.CreateDealershipAsync();
        // 07:00 local on the frozen "today" is already in the past at 08:00.
        var past = new DateTimeOffset(2026, 11, 9, 7, 0, 0, TimeSpan.FromHours(7));
        var id = await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], scenario.TechnicianIds[0], scenario.VehicleId, past);

        var response = await CancelAsync(id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("APPOINTMENT_NOT_CANCELLABLE");
    }

    [Fact]
    public async Task Cancel_UnknownId_Returns404()
    {
        var response = await CancelAsync(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("APPOINTMENT_NOT_FOUND");
    }

    [Fact]
    [Trait("AC", "AC-07.5")]
    [Trait("Category", "Concurrency")]
    public async Task Cancel_TwoConcurrentRequests_ExactlyOneSucceeds()
    {
        var scenario = await _data.CreateDealershipAsync();
        var id = (await BookOkAsync(scenario, scenario.VehicleId)).GetProperty("id").GetGuid();

        var responses = await Task.WhenAll(CancelAsync(id), CancelAsync(id));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
    }
}
