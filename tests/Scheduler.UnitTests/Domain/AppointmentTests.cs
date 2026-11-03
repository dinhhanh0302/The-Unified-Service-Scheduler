using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.UnitTests.Domain.TestData;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Domain;

public sealed class AppointmentTests
{
    private static Appointment ConfirmedAt(DateTimeOffset start)
    {
        var allocation = new Allocation(DomainBuilders.Bay("Bay 1"), DomainBuilders.Tech());

        return Appointment.Confirm(
            Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            dealershipId: DomainBuilders.D1Id,
            DomainBuilders.OilChange(),
            allocation,
            start,
            notes: "Strange noise",
            createdAt: Now);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Confirm_OilChangeAtNine_EndsAtTenAndIsConfirmed()
    {
        var appointment = ConfirmedAt(Tue(9));

        appointment.StartUtc.Should().Be(Tue(9));
        appointment.EndUtc.Should().Be(Tue(10));
        appointment.StartUtc.Offset.Should().Be(TimeSpan.Zero);
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.CancelledAtUtc.Should().BeNull();
    }

    [Fact]
    public void Confirm_NotesLongerThanLimit_Throws()
    {
        var allocation = new Allocation(DomainBuilders.Bay("Bay 1"), DomainBuilders.Tech());

        var act = () => Appointment.Confirm(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DomainBuilders.D1Id, DomainBuilders.OilChange(),
            allocation, Tue(9), new string('x', Appointment.MaxNotesLength + 1), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "AC-07.1")]
    public void Cancel_FutureConfirmed_BecomesCancelled()
    {
        var appointment = ConfirmedAt(Tue(9));

        var result = appointment.Cancel(Now);

        result.IsSuccess.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelledAtUtc.Should().Be(Now);
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public void Cancel_AlreadyCancelled_ReturnsNotCancellable()
    {
        var appointment = ConfirmedAt(Tue(9));
        appointment.Cancel(Now);

        var result = appointment.Cancel(Now);

        result.Error.Should().Be(DomainErrors.AppointmentNotCancellable);
    }

    [Fact]
    [Trait("AC", "AC-07.4")]
    public void Cancel_StartInThePast_ReturnsNotCancellable()
    {
        var appointment = ConfirmedAt(Tue(9));

        var result = appointment.Cancel(Tue(9, 30));

        result.Error.Should().Be(DomainErrors.AppointmentNotCancellable);
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public void Cancel_ExactlyAtStart_ReturnsNotCancellable()
    {
        var appointment = ConfirmedAt(Tue(9));

        appointment.Cancel(Tue(9)).Error.Should().Be(DomainErrors.AppointmentNotCancellable);
    }
}
