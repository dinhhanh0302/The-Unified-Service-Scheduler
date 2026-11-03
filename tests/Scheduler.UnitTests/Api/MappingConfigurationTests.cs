using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Scheduler.Api.Contracts;
using Scheduler.Api.Mapping;
using Scheduler.Application.Appointments;
using Scheduler.Application.ReferenceData;
using Scheduler.Domain.Enums;

namespace Scheduler.UnitTests.Api;

/// <summary>SPEC §12: a missing or renamed member must fail here, not in production.</summary>
public sealed class MappingConfigurationTests
{
    // Same discovery as Program.cs: every profile in the Api assembly.
    private static readonly MapperConfiguration Configuration =
        new(cfg => cfg.AddMaps(typeof(AppointmentProfile).Assembly), NullLoggerFactory.Instance);

    [Fact]
    public void ApiProfiles_Configuration_IsValid()
    {
        Configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void AppointmentDto_MappedToResponse_EveryFieldCarriedOver()
    {
        var offset = TimeSpan.FromHours(7);
        var dto = new AppointmentDto(
            Guid.NewGuid(),
            AppointmentStatus.Confirmed,
            Guid.NewGuid(),
            new VehicleSummary(Guid.NewGuid(), "1HGCM82633A004352"),
            Guid.NewGuid(),
            new ServiceTypeSummary(Guid.NewGuid(), "OIL_CHANGE", 60),
            new TechnicianSummary(Guid.NewGuid(), "Nguyen Van An"),
            new ServiceBaySummary(Guid.NewGuid(), "Bay 1"),
            new DateTimeOffset(2026, 11, 10, 9, 0, 0, offset),
            new DateTimeOffset(2026, 11, 10, 10, 0, 0, offset),
            "Strange noise",
            new DateTimeOffset(2026, 11, 9, 1, 0, 0, TimeSpan.Zero),
            CancelledAtUtc: null);

        var response = Configuration.CreateMapper().Map<AppointmentResponse>(dto);

        response.Should().BeEquivalentTo(
            dto,
            options => options.Excluding(d => d.Status).ComparingRecordsByMembers());
        response.Status.Should().Be("Confirmed");
        response.StartTime.Offset.Should().Be(offset);
    }

    [Fact]
    public void ReferenceDataDtos_MappedToResponses_EnumsBecomeNames()
    {
        var mapper = Configuration.CreateMapper();
        var dealership = new DealershipDto(
            Guid.NewGuid(), "D1", "Asia/Ho_Chi_Minh", new TimeOnly(8, 0), new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Saturday]);
        var serviceType = new ServiceTypeDto(Guid.NewGuid(), "OIL_CHANGE", "Oil change", 60, Qualification.General, BayType.Lift);

        var dealershipResponse = mapper.Map<DealershipResponse>(dealership);
        var serviceTypeResponse = mapper.Map<ServiceTypeDetailsResponse>(serviceType);

        dealershipResponse.OpenDays.Should().Equal("Monday", "Saturday");
        dealershipResponse.OpeningTime.Should().Be(new TimeOnly(8, 0));
        serviceTypeResponse.RequiredQualification.Should().Be("General");
        serviceTypeResponse.RequiredBayType.Should().Be("Lift");
    }
}
