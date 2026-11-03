using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;
using static Scheduler.UnitTests.Domain.TestData.DomainBuilders;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Domain;

public sealed class AvailabilityEngineTests
{
    private readonly Dealership _d1 = D1();
    private readonly ServiceType _oilChange = OilChange();
    private readonly BookingWindow _window = BookingPolicy.GetBookingWindow(Now);

    private static readonly TimeSlot NineToTen = TimeSlot.FromDuration(Tue(9), 60);

    private Allocation? AllocateAtNine(ResourceSnapshot snapshot) =>
        AvailabilityEngine.Allocate(_d1, _oilChange, NineToTen, snapshot);

    // ---- Allocate: FR-02, BR-02 – BR-04 ----

    [Fact]
    [Trait("AC", "AC-01.1")]
    public void Allocate_OneFreeLiftBayAndQualifiedTechnician_ReturnsBoth()
    {
        var bay = Bay("Bay 1");
        var tech = Tech();

        var allocation = AllocateAtNine(Snapshot([bay], [tech]));

        allocation.Should().NotBeNull();
        allocation!.Bay.Should().BeSameAs(bay);
        allocation.Technician.Should().BeSameAs(tech);
    }

    [Fact]
    [Trait("AC", "AC-01.3")]
    public void Allocate_OnlyBayBookedForPartOfWindow_ReturnsNull()
    {
        var bay = Bay("Bay 1");

        var allocation = AllocateAtNine(Snapshot([bay], [Tech()], BayBusy(bay, Tue(9, 30), Tue(10, 30))));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "AC-01.4")]
    public void Allocate_OnlyQualifiedTechnicianBookedForPartOfWindow_ReturnsNull()
    {
        var tech = Tech();

        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1")], [tech], TechBusy(tech, Tue(8, 30), Tue(9, 30))));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "AC-01.5")]
    public void Allocate_OnlyFreeTechnicianLacksRequiredQualification_ReturnsNull()
    {
        var unqualified = Tech(qualifications: [Qualification.Brakes]);

        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1")], [unqualified]));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public void Allocate_ResourcesBookedUntilExactlyStart_ReturnsAllocation()
    {
        var bay = Bay("Bay 1");
        var tech = Tech();

        var allocation = AllocateAtNine(Snapshot([bay], [tech], BothBusy(bay, tech, Tue(8), Tue(9))));

        allocation.Should().NotBeNull();
    }

    [Fact]
    [Trait("AC", "AC-01.7")]
    public void Allocate_OnlyFreeBayHasWrongType_ReturnsNull()
    {
        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1", BayType.General)], [Tech()]));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public void Allocate_TwoFreeTechnicians_PicksTheOneWithFewerBookedMinutes()
    {
        var busier = Tech();
        var idle = Tech();

        var allocation = AllocateAtNine(Snapshot(
            [Bay("Bay 1")],
            [busier, idle],
            TechBusy(busier, Tue(13), Tue(15))));

        allocation!.Technician.Should().BeSameAs(idle);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Allocate_TechniciansWithEqualLoad_PicksLowestId()
    {
        var low = Tech(id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var high = Tech(id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1")], [high, low]));

        allocation!.Technician.Should().BeSameAs(low);
    }

    [Fact]
    [Trait("AC", "AC-01.9")]
    public void Allocate_TwoFreeLiftBays_PicksLowestName()
    {
        var bay2 = Bay("Bay 2");
        var bay1 = Bay("Bay 1");

        var allocation = AllocateAtNine(Snapshot([bay2, bay1], [Tech()]));

        allocation!.Bay.Should().BeSameAs(bay1);
    }

    [Fact]
    [Trait("AC", "AC-01.10")]
    public void Allocate_OnlyInactiveBayAndTechnician_ReturnsNull()
    {
        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1", isActive: false)], [Tech(isActive: false)]));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public void Allocate_ResourcesOfAnotherDealership_AreIgnored()
    {
        var otherDealership = Guid.NewGuid();

        var allocation = AllocateAtNine(Snapshot(
            [Bay("Bay 1", dealershipId: otherDealership)],
            [Tech(dealershipId: otherDealership)]));

        allocation.Should().BeNull();
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public void Allocate_BayFreeButEveryQualifiedTechnicianBusy_ReturnsNull()
    {
        var tech = Tech();

        var allocation = AllocateAtNine(Snapshot([Bay("Bay 1"), Bay("Bay 2")], [tech], TechBusy(tech, Tue(9), Tue(10))));

        allocation.Should().BeNull();
    }

    // ---- GetAvailableStartTimes: FR-06 ----

    [Fact]
    [Trait("AC", "AC-06.1")]
    public void GetAvailableStartTimes_NoBookingsOnTuesday_EveryQuarterHourFromEightToSixteen()
    {
        var starts = AvailabilityEngine.GetAvailableStartTimes(
            _d1, _oilChange, Tuesday, Snapshot([Bay("Bay 1")], [Tech()]), _window);

        starts.Should().HaveCount(33);
        starts[0].Should().Be(Tue(8));
        starts[^1].Should().Be(Tue(16));
        starts.Should().BeInAscendingOrder();
    }

    [Fact]
    [Trait("AC", "AC-06.2")]
    public void GetAvailableStartTimes_AllLiftBaysBookedTenToTwelve_ExcludesOverlappingStarts()
    {
        var bay1 = Bay("Bay 1");
        var bay2 = Bay("Bay 2");

        var starts = AvailabilityEngine.GetAvailableStartTimes(
            _d1,
            _oilChange,
            Tuesday,
            Snapshot([bay1, bay2], [Tech(), Tech()], BayBusy(bay1, Tue(10), Tue(12)), BayBusy(bay2, Tue(10), Tue(12))),
            _window);

        starts.Should().Contain(new[] { Tue(9), Tue(12) });
        starts.Should().NotContain(start => start > Tue(9) && start < Tue(12));
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public void GetAvailableStartTimes_Sunday_ReturnsEmpty()
    {
        var starts = AvailabilityEngine.GetAvailableStartTimes(
            _d1, _oilChange, Sunday, Snapshot([Bay("Bay 1")], [Tech()]), _window);

        starts.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-06.4")]
    public void GetAvailableStartTimes_Today_FirstStartRespectsMinimumLeadTime()
    {
        var starts = AvailabilityEngine.GetAvailableStartTimes(
            _d1, _oilChange, Monday, Snapshot([Bay("Bay 1")], [Tech()]), _window);

        starts[0].Should().Be(Mon(9));
    }

    // ---- SuggestStartTimes: FR-05 ----

    [Fact]
    [Trait("AC", "AC-05.1")]
    public void SuggestStartTimes_RequestedSlotTaken_ReturnsThreeNearestLaterStarts()
    {
        // Single Lift bay; free windows make 10:00, 11:15 and 13:00 the first available starts after 09:00.
        var bay = Bay("Bay 1");
        var snapshot = Snapshot(
            [bay],
            [Tech()],
            BayBusy(bay, Tue(8), Tue(10)),
            BayBusy(bay, Tue(11), Tue(11, 15)),
            BayBusy(bay, Tue(12, 15), Tue(13)));

        var suggestions = AvailabilityEngine.SuggestStartTimes(_d1, _oilChange, NineToTen, snapshot, [], _window);

        suggestions.Should().Equal(Tue(10), Tue(11, 15), Tue(13));
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    public void SuggestStartTimes_NothingFreeForRestOfDay_ReturnsEmpty()
    {
        var bay = Bay("Bay 1");

        var suggestions = AvailabilityEngine.SuggestStartTimes(
            _d1, _oilChange, NineToTen, Snapshot([bay], [Tech()], BayBusy(bay, Tue(8), Tue(17))), [], _window);

        suggestions.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    public void SuggestStartTimes_VehicleBookedElsewhere_SkipsOverlappingStarts()
    {
        TimeSlot[] vehicleBookings = [new TimeSlot(Tue(10), Tue(11))];

        var suggestions = AvailabilityEngine.SuggestStartTimes(
            _d1, _oilChange, NineToTen, Snapshot([Bay("Bay 1")], [Tech()]), vehicleBookings, _window);

        suggestions.Should().NotContain(Tue(10));
        suggestions.Should().Equal(Tue(11), Tue(11, 15), Tue(11, 30));
    }
}
