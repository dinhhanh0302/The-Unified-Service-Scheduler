namespace Scheduler.Application.Appointments.Book;

/// <summary>
/// Raw booking request as received from the API. Ids are nullable and <see cref="StartTime"/> is the raw
/// string so the validator can tell "missing" from "malformed" and detect a missing UTC offset (OQ-09).
/// </summary>
public sealed record BookAppointmentCommand(
    Guid? CustomerId,
    Guid? VehicleId,
    Guid? DealershipId,
    Guid? ServiceTypeId,
    string? StartTime,
    string? Notes,
    string? IdempotencyKey);

/// <summary>A command that passed validation steps 1–2 of the error evaluation order (SPEC §5).</summary>
public sealed record ValidatedBooking(
    Guid CustomerId,
    Guid VehicleId,
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTimeOffset Start,
    string? Notes,
    string? IdempotencyKey);
