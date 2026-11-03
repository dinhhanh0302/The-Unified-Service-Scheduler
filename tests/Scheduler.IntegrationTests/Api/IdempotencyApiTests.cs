using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>FR-08 end to end (SPEC §7, AC-08.x).</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Api")]
public sealed class IdempotencyApiTests(SchedulerApiFactory factory)
{
    private const string Url = "/api/v1/appointments";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiTestData _data = new(factory);

    private static string NewKey() => $"key-{Guid.NewGuid():N}";

    private async Task<HttpResponseMessage> BookAsync(Scenario scenario, string key, string startTime = "2026-11-10T09:00:00+07:00")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = JsonContent.Create(new
            {
                customerId = scenario.CustomerId,
                vehicleId = scenario.VehicleId,
                dealershipId = scenario.DealershipId,
                serviceTypeId = ApiTestData.OilChange,
                startTime
            })
        };
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task Post_SameKeyRetried_Returns201WithSameIdAndNoSecondRecord()
    {
        var scenario = await _data.CreateDealershipAsync();
        var key = NewKey();
        var first = await BookAsync(scenario, key);

        var retry = await BookAsync(scenario, key);

        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        (await JsonAsync(retry)).GetProperty("id").GetGuid().Should().Be((await JsonAsync(first)).GetProperty("id").GetGuid());
        retry.Headers.GetValues("Idempotency-Replayed").Single().Should().Be("true");
        first.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
        (await _data.CountConfirmedAsync(scenario.DealershipId)).Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-08.2")]
    public async Task Post_SameKeyDifferentBody_Returns422()
    {
        var scenario = await _data.CreateDealershipAsync();
        var key = NewKey();
        await BookAsync(scenario, key);

        var response = await BookAsync(scenario, key, "2026-11-10T13:00:00+07:00");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    [Trait("AC", "AC-08.3")]
    public async Task Post_KeyOfARejectedRequest_IsReEvaluatedOnRetry()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 1, technicians: 1);
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);
        var blocking = await _data.BookDirectlyAsync(
            scenario, scenario.BayIds[0], scenario.TechnicianIds[0], otherVehicle,
            new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(7)));
        var key = NewKey();
        (await BookAsync(scenario, key)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PostAsync($"{Url}/{blocking}/cancel", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var retry = await BookAsync(scenario, key);

        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    [Trait("Category", "Concurrency")]
    public async Task Post_FiveParallelIdenticalRequests_AllReturnTheSameAppointment()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 5, technicians: 5);
        var key = NewKey();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => BookAsync(scenario, key)));

        var bodies = await Task.WhenAll(responses.Select(JsonAsync));
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created,
            "every retry of the same request must get the original; bodies: {0}", string.Join(" | ", bodies));
        bodies.Select(b => b.GetProperty("id").GetGuid()).Distinct().Should().ContainSingle();
        (await _data.CountConfirmedAsync(scenario.DealershipId)).Should().Be(1);
    }
}
