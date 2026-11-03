using Microsoft.AspNetCore.Http;
using Scheduler.Api.ErrorHandling;
using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Common;
using Scheduler.Domain.Results;

namespace Scheduler.UnitTests.Api;

/// <summary>Error catalog (SPEC §8): every code maps to its documented HTTP status.</summary>
public sealed class ProblemResultsTests
{
    [Theory]
    [InlineData("VALIDATION_FAILED", 400)]
    [InlineData("INVALID_START_TIME", 400)]
    [InlineData("CUSTOMER_NOT_FOUND", 404)]
    [InlineData("VEHICLE_NOT_FOUND", 404)]
    [InlineData("DEALERSHIP_NOT_FOUND", 404)]
    [InlineData("SERVICE_TYPE_NOT_FOUND", 404)]
    [InlineData("APPOINTMENT_NOT_FOUND", 404)]
    [InlineData("SLOT_UNAVAILABLE", 409)]
    [InlineData("VEHICLE_ALREADY_BOOKED", 409)]
    [InlineData("APPOINTMENT_NOT_CANCELLABLE", 409)]
    [InlineData("OUTSIDE_OPENING_HOURS", 422)]
    [InlineData("OUTSIDE_BOOKING_HORIZON", 422)]
    [InlineData("VEHICLE_NOT_OWNED", 422)]
    [InlineData("IDEMPOTENCY_KEY_REUSED", 422)]
    [InlineData("SOMETHING_UNKNOWN", 500)]
    public void StatusFor_CatalogCode_ReturnsDocumentedStatus(string code, int expectedStatus)
    {
        ProblemResults.StatusFor(code).Should().Be(expectedStatus);
    }

    [Fact]
    public void ToProblemDetails_AnyError_CarriesCodeCorrelationIdAndType()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "corr-123" };

        var problem = ProblemResults.ToProblemDetails(DomainErrors.VehicleNotOwned, httpContext);

        problem.Status.Should().Be(422);
        problem.Type.Should().Be("https://errors.scheduler/vehicle-not-owned");
        problem.Extensions["code"].Should().Be("VEHICLE_NOT_OWNED");
        problem.Extensions["correlationId"].Should().Be("corr-123");
    }

    [Fact]
    public void ToProblemDetails_SlotUnavailable_IncludesSuggestedStartTimes()
    {
        var suggestion = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(7));

        var problem = ProblemResults.ToProblemDetails(new SlotUnavailableError([suggestion]), new DefaultHttpContext());

        problem.Status.Should().Be(409);
        problem.Extensions["suggestedStartTimes"].Should().BeEquivalentTo(new[] { suggestion });
    }

    [Fact]
    public void ToProblemDetails_ValidationFailed_IncludesFieldErrors()
    {
        var errors = new Dictionary<string, string[]> { ["customerId"] = ["customerId is required."] };

        var problem = ProblemResults.ToProblemDetails(new ValidationFailedError(errors), new DefaultHttpContext());

        problem.Status.Should().Be(400);
        problem.Extensions["errors"].Should().BeSameAs(errors);
    }
}
