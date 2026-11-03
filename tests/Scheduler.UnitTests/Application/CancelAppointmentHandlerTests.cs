using Scheduler.Application.Abstractions;
using Scheduler.Application.Appointments.Cancel;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.UnitTests.Application.Fakes;
using Scheduler.UnitTests.Domain.TestData;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Application;

public sealed class CancelAppointmentHandlerTests
{
    private readonly InMemoryStores _stores = new();
    private readonly Dealership _d1 = DomainBuilders.D1();
    private readonly ServiceType _oilChange = DomainBuilders.OilChange();
    private readonly ServiceBay _bay = DomainBuilders.Bay("Bay 1");
    private readonly Technician _technician = DomainBuilders.Tech();
    private readonly Customer _customer = new(Guid.NewGuid(), "An Nguyen", "an@example.com", "1");
    private readonly Vehicle _vehicle;

    public CancelAppointmentHandlerTests()
    {
        _vehicle = new Vehicle(Guid.NewGuid(), "1HGCM82633A004352", "Honda", "Accord", 2003, _customer.Id);
        _stores.Dealerships.Add(_d1);
        _stores.ServiceTypes.Add(_oilChange);
        _stores.Bays.Add(_bay);
        _stores.Technicians.Add(_technician);
        _stores.Customers.Add(_customer);
        _stores.Vehicles.Add(_vehicle);
    }

    private CancelAppointmentHandler Handler => new(_stores, _stores, new FixedTimeProvider(Now));

    private Appointment Booked(DateTimeOffset start)
    {
        var appointment = Appointment.Confirm(
            Guid.NewGuid(), _customer.Id, _vehicle.Id, _d1.Id, _oilChange,
            new Allocation(_bay, _technician), start, notes: null, createdAt: Now.AddDays(-1));
        _stores.Appointments.Add(appointment);
        return appointment;
    }

    [Fact]
    [Trait("AC", "AC-07.1")]
    public async Task Handle_FutureConfirmed_ReturnsCancelledAppointment()
    {
        var appointment = Booked(Tue(9));

        var result = await Handler.HandleAsync(appointment.Id);

        result.Value.Status.Should().Be(AppointmentStatus.Cancelled);
        result.Value.CancelledAtUtc.Should().Be(Now);
        result.Value.StartTime.Should().Be(Tue(9));
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public async Task Handle_AlreadyCancelled_ReturnsNotCancellable()
    {
        var appointment = Booked(Tue(9));
        await Handler.HandleAsync(appointment.Id);

        var result = await Handler.HandleAsync(appointment.Id);

        result.Error.Should().Be(DomainErrors.AppointmentNotCancellable);
    }

    [Fact]
    [Trait("AC", "AC-07.4")]
    public async Task Handle_StartInThePast_ReturnsNotCancellable()
    {
        var appointment = Booked(Mon(7));

        var result = await Handler.HandleAsync(appointment.Id);

        result.Error.Should().Be(DomainErrors.AppointmentNotCancellable);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsAppointmentNotFound()
    {
        var result = await Handler.HandleAsync(Guid.NewGuid());

        result.Error.Should().Be(DomainErrors.AppointmentNotFound);
    }

    [Fact]
    [Trait("AC", "AC-07.5")]
    public async Task Handle_RowChangedConcurrently_ReturnsNotCancellable()
    {
        var appointment = Booked(Tue(9));
        _stores.ForcedUpdateResults.Enqueue(UpdateResult.ConcurrencyConflict);

        var result = await Handler.HandleAsync(appointment.Id);

        result.Error.Should().Be(DomainErrors.AppointmentNotCancellable);
    }
}
