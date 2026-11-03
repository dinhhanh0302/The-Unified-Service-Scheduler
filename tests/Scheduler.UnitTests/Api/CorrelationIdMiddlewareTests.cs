using Scheduler.Api.Middleware;

namespace Scheduler.UnitTests.Api;

[Trait("Category", "Platform")]
public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("abc-123")]
    [InlineData("order_42.retry")]
    [InlineData("0f8e3c7a9b2d4e6f8a0b1c2d3e4f5a6b")]
    public void ResolveCorrelationId_ValidIncomingValue_IsKept(string incoming)
    {
        CorrelationIdMiddleware.ResolveCorrelationId(incoming).Should().Be(incoming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("contains space")]
    [InlineData("line\r\nbreak")]
    [InlineData("<script>")]
    public void ResolveCorrelationId_MissingOrUnsafeValue_GeneratesNewId(string? incoming)
    {
        var result = CorrelationIdMiddleware.ResolveCorrelationId(incoming);

        result.Should().NotBe(incoming);
        Guid.TryParseExact(result, "N", out _).Should().BeTrue();
    }

    [Fact]
    public void ResolveCorrelationId_TooLongValue_GeneratesNewId()
    {
        var incoming = new string('a', 65);

        CorrelationIdMiddleware.ResolveCorrelationId(incoming).Should().NotBe(incoming);
    }
}
