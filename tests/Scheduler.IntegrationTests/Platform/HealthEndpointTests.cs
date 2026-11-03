using System.Net;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Platform;

[Collection(ApiCollection.Name)]
[Trait("Category", "Platform")]
public sealed class HealthEndpointTests(SchedulerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthLive_ProcessRunning_Returns200()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_DatabaseReachable_Returns200()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnyRequest_WithoutCorrelationId_ResponseCarriesGeneratedId()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnyRequest_WithCorrelationId_ResponseEchoesSameId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be("test-correlation-123");
    }

    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
