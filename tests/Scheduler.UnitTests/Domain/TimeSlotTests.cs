using Scheduler.Domain.ValueObjects;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Domain;

public sealed class TimeSlotTests
{
    [Fact]
    public void Constructor_EndNotAfterStart_Throws()
    {
        var act = () => new TimeSlot(Tue(9), Tue(9));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_AnyOffset_StoresUtc()
    {
        var slot = new TimeSlot(Tue(9), Tue(10));

        slot.StartUtc.Offset.Should().Be(TimeSpan.Zero);
        slot.StartUtc.Should().Be(Tue(9));
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void FromDuration_SixtyMinutes_EndIsStartPlusDuration()
    {
        var slot = TimeSlot.FromDuration(Tue(9), 60);

        slot.EndUtc.Should().Be(Tue(10));
        slot.Duration.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public void Overlaps_BackToBackWindows_ReturnsFalse()
    {
        var earlier = new TimeSlot(Tue(8), Tue(9));
        var later = new TimeSlot(Tue(9), Tue(10));

        earlier.Overlaps(later).Should().BeFalse();
        later.Overlaps(earlier).Should().BeFalse();
    }

    [Theory]
    [InlineData(9, 30, 10, 30)]  // overlaps the end
    [InlineData(8, 30, 9, 30)]   // overlaps the start
    [InlineData(9, 15, 9, 45)]   // inside
    [InlineData(8, 0, 11, 0)]    // contains
    public void Overlaps_PartialOrFullOverlap_ReturnsTrue(int startHour, int startMinute, int endHour, int endMinute)
    {
        var requested = new TimeSlot(Tue(9), Tue(10));
        var other = new TimeSlot(Tue(startHour, startMinute), Tue(endHour, endMinute));

        requested.Overlaps(other).Should().BeTrue();
    }
}
