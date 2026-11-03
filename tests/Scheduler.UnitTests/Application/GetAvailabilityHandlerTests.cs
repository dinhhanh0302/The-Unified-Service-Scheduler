using Scheduler.Application.Availability;
using Scheduler.Application.Common;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Results;
using Scheduler.UnitTests.Application.Fakes;
using Scheduler.UnitTests.Domain.TestData;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Application;

public sealed class GetAvailabilityHandlerTests
{
    private readonly InMemoryStores _stores = new();
    private readonly Dealership _d1 = DomainBuilders.D1();
    private readonly ServiceType _oilChange = DomainBuilders.OilChange();

    public GetAvailabilityHandlerTests()
    {
        _stores.Dealerships.Add(_d1);
        _stores.ServiceTypes.Add(_oilChange);
        _stores.Bays.Add(DomainBuilders.Bay("Bay 1"));
        _stores.Technicians.Add(DomainBuilders.Tech());
    }

    private GetAvailabilityHandler Handler => new(_stores, _stores, new FixedTimeProvider(Now));

    [Fact]
    [Trait("AC", "AC-06.1")]
    public async Task Handle_FreeTuesday_ReturnsQuarterHoursInDealershipOffset()
    {
        var result = await Handler.HandleAsync(new GetAvailabilityQuery(_d1.Id, _oilChange.Id, Tuesday));

        result.Value.Date.Should().Be(Tuesday);
        result.Value.StartTimes.Should().HaveCount(33);
        result.Value.StartTimes[0].Should().Be(Tue(8));
        result.Value.StartTimes.Should().OnlyContain(t => t.Offset == TimeSpan.FromHours(7));
    }

    [Fact]
    [Trait("AC", "AC-06.4")]
    public async Task Handle_Today_FirstStartRespectsMinimumLeadTime()
    {
        var result = await Handler.HandleAsync(new GetAvailabilityQuery(_d1.Id, _oilChange.Id, Monday));

        result.Value.StartTimes[0].Should().Be(Mon(9));
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task Handle_UnknownDealership_ReturnsDealershipNotFound()
    {
        var result = await Handler.HandleAsync(new GetAvailabilityQuery(Guid.NewGuid(), _oilChange.Id, Tuesday));

        result.Error.Should().Be(DomainErrors.DealershipNotFound);
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task Handle_UnknownServiceType_ReturnsServiceTypeNotFound()
    {
        var result = await Handler.HandleAsync(new GetAvailabilityQuery(_d1.Id, Guid.NewGuid(), Tuesday));

        result.Error.Should().Be(DomainErrors.ServiceTypeNotFound);
    }

    [Fact]
    public async Task Handle_MissingParameters_ReturnsValidationFailedPerField()
    {
        var result = await Handler.HandleAsync(new GetAvailabilityQuery(null, Guid.Empty, null));

        result.Error.Should().BeOfType<ValidationFailedError>()
            .Which.Errors.Keys.Should().BeEquivalentTo(new[] { "dealershipId", "serviceTypeId", "date" });
    }
}
