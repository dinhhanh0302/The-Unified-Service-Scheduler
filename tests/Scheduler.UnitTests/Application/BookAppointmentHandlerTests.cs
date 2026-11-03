using Scheduler.Application.Abstractions;
using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Common;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.UnitTests.Application.Fakes;
using Scheduler.UnitTests.Domain.TestData;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Application;

public sealed class BookAppointmentHandlerTests
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly InMemoryStores _stores = new();
    private readonly Dealership _d1 = DomainBuilders.D1();
    private readonly Dealership _d2;
    private readonly ServiceType _oilChange = DomainBuilders.OilChange();
    private readonly Customer _customer = new(Guid.NewGuid(), "An Nguyen", "an@example.com", "1");
    private readonly Customer _otherCustomer = new(Guid.NewGuid(), "Binh Tran", "binh@example.com", "2");
    private readonly Vehicle _vehicle;
    private readonly Vehicle _otherCustomersVehicle;
    private readonly ServiceBay _bay = DomainBuilders.Bay("Bay 1");
    private readonly Technician _technician = DomainBuilders.Tech();

    public BookAppointmentHandlerTests()
    {
        _d2 = new Dealership(Guid.NewGuid(), "D2", "Asia/Ho_Chi_Minh", new TimeOnly(8, 0), new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        _vehicle = new Vehicle(Guid.NewGuid(), "1HGCM82633A004352", "Honda", "Accord", 2003, _customer.Id);
        _otherCustomersVehicle = new Vehicle(Guid.NewGuid(), "5YJ3E1EA7KF317000", "Tesla", "Model 3", 2019, _otherCustomer.Id);

        _stores.Dealerships.AddRange([_d1, _d2]);
        _stores.ServiceTypes.Add(_oilChange);
        _stores.Customers.AddRange([_customer, _otherCustomer]);
        _stores.Vehicles.AddRange([_vehicle, _otherCustomersVehicle]);
        _stores.Bays.Add(_bay);
        _stores.Technicians.Add(_technician);
    }

    private BookAppointmentHandler Handler => new(_stores, _stores, new FixedTimeProvider(Now));

    private BookAppointmentCommand Command(
        string start = "2026-11-10T09:00:00+07:00",
        Guid? customerId = null,
        Guid? vehicleId = null,
        Guid? dealershipId = null,
        Guid? serviceTypeId = null,
        string? idempotencyKey = null,
        string? notes = null) =>
        new(
            customerId ?? _customer.Id,
            vehicleId ?? _vehicle.Id,
            dealershipId ?? _d1.Id,
            serviceTypeId ?? _oilChange.Id,
            start,
            notes,
            idempotencyKey);

    /// <summary>An existing confirmed booking (another vehicle unless given) occupying the bay and/or technician.</summary>
    private Appointment AddBooking(
        DateTimeOffset start, Guid? bayId = null, Guid? technicianId = null, Guid? vehicleId = null, Dealership? dealership = null)
    {
        var bay = DomainBuilders.Bay("Existing", id: bayId ?? Guid.NewGuid(), dealershipId: (dealership ?? _d1).Id);
        var technician = DomainBuilders.Tech(id: technicianId ?? Guid.NewGuid(), dealershipId: (dealership ?? _d1).Id);
        var appointment = Appointment.Confirm(
            Guid.NewGuid(), _customer.Id, vehicleId ?? Guid.NewGuid(), (dealership ?? _d1).Id, _oilChange,
            new Allocation(bay, technician), start, notes: null, createdAt: Now);
        _stores.Appointments.Add(appointment);
        return appointment;
    }

    // ---- Happy path: FR-01, FR-03 ----

    [Fact]
    [Trait("AC", "AC-01.1")]
    public async Task Handle_FreeBayAndTechnician_ConfirmsAndStoresAppointment()
    {
        var result = await Handler.HandleAsync(Command());

        result.IsSuccess.Should().BeTrue();
        var booking = result.Value;
        booking.Replayed.Should().BeFalse();
        booking.Appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        booking.Appointment.StartTime.Should().Be(Tue(9));
        booking.Appointment.StartTime.Offset.Should().Be(VietnamOffset);
        booking.Appointment.EndTime.Should().Be(Tue(10));
        booking.Appointment.Technician.Id.Should().Be(_technician.Id);
        booking.Appointment.ServiceBay.Name.Should().Be("Bay 1");
        booking.Appointment.Vehicle.Vin.Should().Be(_vehicle.Vin);
        _stores.Appointments.Should().ContainSingle().Which.RequestHash.Should().NotBeNullOrEmpty();
    }

    // ---- FR-02 / FR-05: unavailable slot with suggestions ----

    [Fact]
    [Trait("AC", "AC-01.3")]
    [Trait("AC", "AC-05.1")]
    public async Task Handle_OnlyBayBusyForPartOfWindow_ReturnsSlotUnavailableWithSuggestions()
    {
        AddBooking(Tue(9, 30), bayId: _bay.Id);

        var result = await Handler.HandleAsync(Command());

        var error = result.Error.Should().BeOfType<SlotUnavailableError>().Subject;
        error.Code.Should().Be("SLOT_UNAVAILABLE");
        error.SuggestedStartTimes.Should().Equal(Tue(10, 30), Tue(10, 45), Tue(11));
        error.SuggestedStartTimes.Should().OnlyContain(t => t.Offset == VietnamOffset);
        _stores.InsertCalls.Should().Be(0);
    }

    // ---- Policy checks ----

    [Fact]
    [Trait("AC", "AC-01.11")]
    public async Task Handle_EndsAfterClosing_ReturnsOutsideOpeningHours()
    {
        var result = await Handler.HandleAsync(Command("2026-11-10T16:30:00+07:00"));

        result.Error.Should().Be(DomainErrors.OutsideOpeningHours);
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public async Task Handle_ThirtyMinutesFromNow_ReturnsOutsideBookingHorizon()
    {
        var result = await Handler.HandleAsync(Command("2026-11-09T08:30:00+07:00"));

        result.Error.Should().Be(DomainErrors.OutsideBookingHorizon);
    }

    [Fact]
    [Trait("AC", "AC-01.17")]
    public async Task Handle_VehicleOfAnotherCustomer_ReturnsVehicleNotOwned()
    {
        var result = await Handler.HandleAsync(Command(vehicleId: _otherCustomersVehicle.Id));

        result.Error.Should().Be(DomainErrors.VehicleNotOwned);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Handle_VehicleBookedAtAnotherDealership_ReturnsVehicleAlreadyBooked()
    {
        AddBooking(Tue(9, 30), vehicleId: _vehicle.Id, dealership: _d2);

        var result = await Handler.HandleAsync(Command());

        result.Error.Should().Be(DomainErrors.VehicleAlreadyBooked);
    }

    [Theory]
    [InlineData("customer", "CUSTOMER_NOT_FOUND")]
    [InlineData("vehicle", "VEHICLE_NOT_FOUND")]
    [InlineData("dealership", "DEALERSHIP_NOT_FOUND")]
    [InlineData("serviceType", "SERVICE_TYPE_NOT_FOUND")]
    [Trait("AC", "AC-01.19")]
    public async Task Handle_UnknownReference_ReturnsMatchingNotFound(string unknown, string expectedCode)
    {
        var missing = Guid.NewGuid();
        var command = unknown switch
        {
            "customer" => Command(customerId: missing),
            "vehicle" => Command(vehicleId: missing),
            "dealership" => Command(dealershipId: missing),
            _ => Command(serviceTypeId: missing)
        };

        var result = await Handler.HandleAsync(command);

        result.Error!.Code.Should().Be(expectedCode);
    }

    // ---- Error evaluation order (SPEC §5) ----

    [Fact]
    public async Task Handle_MissingFields_ReturnsValidationFailed()
    {
        var result = await Handler.HandleAsync(new BookAppointmentCommand(null, null, null, null, null, null, null));

        result.Error.Should().BeOfType<ValidationFailedError>();
    }

    [Fact]
    public async Task Handle_UnknownCustomerAndMisalignedStart_ReportsInvalidStartTimeFirst()
    {
        var result = await Handler.HandleAsync(Command("2026-11-10T09:10:00+07:00", customerId: Guid.NewGuid()));

        result.Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    [Fact]
    public async Task Handle_VehicleNotOwnedAndOutsideHorizon_ReportsVehicleNotOwnedFirst()
    {
        var result = await Handler.HandleAsync(
            Command("2026-11-09T08:30:00+07:00", vehicleId: _otherCustomersVehicle.Id));

        result.Error.Should().Be(DomainErrors.VehicleNotOwned);
    }

    [Fact]
    public async Task Handle_ClosedDayInThePast_ReportsHorizonBeforeOpeningHours()
    {
        // Sunday 2026-11-08 is both closed and in the past; OQ-10: horizon is checked first.
        var result = await Handler.HandleAsync(Command("2026-11-08T09:00:00+07:00"));

        result.Error.Should().Be(DomainErrors.OutsideBookingHorizon);
    }

    // ---- Concurrency: retry on constraint conflicts (SPEC §10) ----

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Handle_ResourceConflictOnFirstInsert_RetriesAndSucceeds()
    {
        _stores.ForcedInsertResults.Enqueue(InsertResult.ResourceConflict);

        var result = await Handler.HandleAsync(Command());

        result.IsSuccess.Should().BeTrue();
        _stores.InsertCalls.Should().Be(2);
        _stores.Appointments.Should().ContainSingle();
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Handle_ResourceConflictOnEveryAttempt_ReturnsSlotUnavailableAfterThreeAttempts()
    {
        for (var i = 0; i < BookAppointmentHandler.MaxAttempts; i++)
        {
            _stores.ForcedInsertResults.Enqueue(InsertResult.ResourceConflict);
        }

        var result = await Handler.HandleAsync(Command());

        result.Error.Should().BeOfType<SlotUnavailableError>();
        _stores.InsertCalls.Should().Be(BookAppointmentHandler.MaxAttempts);
        _stores.Appointments.Should().BeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task Handle_VehicleConflictOnInsert_ReturnsVehicleAlreadyBookedWithoutRetry()
    {
        _stores.ForcedInsertResults.Enqueue(InsertResult.VehicleConflict);

        var result = await Handler.HandleAsync(Command());

        result.Error.Should().Be(DomainErrors.VehicleAlreadyBooked);
        _stores.InsertCalls.Should().Be(1);
    }

    // ---- Idempotency: FR-08 ----

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task Handle_SameKeyAndBodyRetried_ReplaysOriginalWithoutNewRecord()
    {
        var first = await Handler.HandleAsync(Command(idempotencyKey: "K1"));

        var retry = await Handler.HandleAsync(Command(idempotencyKey: "K1"));

        retry.Value.Replayed.Should().BeTrue();
        retry.Value.Appointment.Id.Should().Be(first.Value.Appointment.Id);
        _stores.Appointments.Should().ContainSingle();
        _stores.InsertCalls.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task Handle_SameKeySameInstantDifferentOffset_IsTheSameRequest()
    {
        await Handler.HandleAsync(Command(idempotencyKey: "K1"));

        var retry = await Handler.HandleAsync(Command("2026-11-10T02:00:00Z", idempotencyKey: "K1"));

        retry.Value.Replayed.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "AC-08.2")]
    public async Task Handle_SameKeyDifferentBody_ReturnsIdempotencyKeyReused()
    {
        await Handler.HandleAsync(Command(idempotencyKey: "K1"));

        var result = await Handler.HandleAsync(Command("2026-11-10T11:00:00+07:00", idempotencyKey: "K1"));

        result.Error.Should().Be(DomainErrors.IdempotencyKeyReused);
    }

    [Fact]
    [Trait("AC", "AC-08.3")]
    public async Task Handle_KeyOfAFailedRequest_IsReEvaluatedOnRetry()
    {
        var blocking = AddBooking(Tue(9), bayId: _bay.Id);
        var first = await Handler.HandleAsync(Command(idempotencyKey: "K2"));
        first.Error.Should().BeOfType<SlotUnavailableError>();

        blocking.Cancel(Now);
        var retry = await Handler.HandleAsync(Command(idempotencyKey: "K2"));

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Replayed.Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Handle_ConcurrentRequestWithSameKeyWonTheInsert_ReplaysTheWinner()
    {
        _stores.ForcedInsertResults.Enqueue(InsertResult.IdempotencyKeyConflict);

        var result = await Handler.HandleAsync(Command(idempotencyKey: "K3"));

        result.Value.Replayed.Should().BeTrue();
        _stores.Appointments.Should().ContainSingle();
    }
}
