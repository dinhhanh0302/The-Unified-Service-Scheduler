using Scheduler.Domain.Entities;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;
using Scheduler.UnitTests.Domain.TestData;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Domain;

public sealed class BookingPolicyTests
{
    private readonly Dealership _d1 = DomainBuilders.D1();

    // ---- BR-07 slot granularity ----

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [Trait("BR", "BR-07")]
    public void CheckSlotAlignment_QuarterHour_Succeeds(int minute)
    {
        BookingPolicy.CheckSlotAlignment(Tue(9, minute)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "AC-01.15")]
    public void CheckSlotAlignment_NineTen_ReturnsInvalidStartTime()
    {
        var result = BookingPolicy.CheckSlotAlignment(Tue(9, 10));

        result.Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    [Fact]
    [Trait("AC", "AC-01.15")]
    public void CheckSlotAlignment_ThirtySeconds_ReturnsInvalidStartTime()
    {
        var result = BookingPolicy.CheckSlotAlignment(Tue(9).AddSeconds(30));

        result.Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    [Fact]
    [Trait("AC", "AC-01.15")]
    public void CheckSlotAlignment_FractionalSecond_ReturnsInvalidStartTime()
    {
        var result = BookingPolicy.CheckSlotAlignment(Tue(9).AddMilliseconds(500));

        result.Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    // ---- BR-06 booking horizon ----

    [Fact]
    [Trait("AC", "AC-01.13")]
    public void CheckHorizon_ThirtyMinutesFromNow_ReturnsOutsideBookingHorizon()
    {
        var result = BookingPolicy.CheckHorizon(Mon(8, 30), Now);

        result.Error.Should().Be(DomainErrors.OutsideBookingHorizon);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public void CheckHorizon_ExactlySixtyMinutesFromNow_Succeeds()
    {
        BookingPolicy.CheckHorizon(Mon(9), Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public void CheckHorizon_ExactlySixtyDaysAhead_Succeeds()
    {
        BookingPolicy.CheckHorizon(Now.AddDays(60), Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    public void CheckHorizon_SixtyOneDaysAhead_ReturnsOutsideBookingHorizon()
    {
        var result = BookingPolicy.CheckHorizon(Now.AddDays(61), Now);

        result.Error.Should().Be(DomainErrors.OutsideBookingHorizon);
    }

    // ---- BR-05 opening hours ----

    [Fact]
    [Trait("AC", "AC-01.11")]
    public void CheckOpeningHours_EndsAfterClosing_ReturnsOutsideOpeningHours()
    {
        var slot = TimeSlot.FromDuration(Tue(16, 30), 60);

        BookingPolicy.CheckOpeningHours(_d1, slot).Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_EndsExactlyAtClosing_Succeeds()
    {
        var slot = TimeSlot.FromDuration(Tue(16), 60);

        BookingPolicy.CheckOpeningHours(_d1, slot).IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_StartsBeforeOpening_ReturnsOutsideOpeningHours()
    {
        var slot = TimeSlot.FromDuration(Tue(7, 45), 60);

        BookingPolicy.CheckOpeningHours(_d1, slot).Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("AC", "AC-01.12")]
    public void CheckOpeningHours_Sunday_ReturnsOutsideOpeningHours()
    {
        var slot = TimeSlot.FromDuration(Sun(9), 60);

        BookingPolicy.CheckOpeningHours(_d1, slot).Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_SameInstantSentInUtc_EvaluatedInDealershipTimeZone()
    {
        // 02:00Z is 09:00 in Asia/Ho_Chi_Minh, so this is inside opening hours.
        var startInUtc = new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero);

        var result = BookingPolicy.CheckOpeningHours(_d1, TimeSlot.FromDuration(startInUtc, 60));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_UtcInstantEndingAfterClosingLocally_ReturnsOutsideOpeningHours()
    {
        // 09:00Z is 16:00 local; a 120-minute job would end at 18:00 local.
        var startInUtc = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        var result = BookingPolicy.CheckOpeningHours(_d1, TimeSlot.FromDuration(startInUtc, 120));

        result.Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_AmbiguousLocalHourDuringDstFallBack_ReturnsOutsideOpeningHours()
    {
        // America/New_York falls back on Sun 2026-11-01 at 02:00 EDT → 01:00 EST; 01:00–02:00 local occurs twice.
        var newYork = new Dealership(
            Guid.NewGuid(), "NY", "America/New_York", new TimeOnly(0, 0), new TimeOnly(4, 0), [DayOfWeek.Sunday]);
        var ambiguousStart = new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero); // 01:30 EDT

        var result = BookingPolicy.CheckOpeningHours(newYork, TimeSlot.FromDuration(ambiguousStart, 60));

        result.Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void CheckOpeningHours_AfterDstFallBack_Succeeds()
    {
        var newYork = new Dealership(
            Guid.NewGuid(), "NY", "America/New_York", new TimeOnly(0, 0), new TimeOnly(4, 0), [DayOfWeek.Sunday]);
        var start = new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero); // 02:00 EST

        var result = BookingPolicy.CheckOpeningHours(newYork, TimeSlot.FromDuration(start, 60));

        result.IsSuccess.Should().BeTrue();
    }
}
