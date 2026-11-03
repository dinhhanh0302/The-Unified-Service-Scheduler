using Scheduler.Application.Appointments.Book;

namespace Scheduler.Api.Contracts;

/// <summary>
/// POST /api/v1/appointments body (SPEC §8). All fields are nullable so missing values reach the validator
/// and come back as VALIDATION_FAILED field errors; <see cref="StartTime"/> stays a string so a missing UTC
/// offset can be detected (OQ-09).
/// </summary>
public sealed record BookAppointmentRequest(
    Guid? CustomerId,
    Guid? VehicleId,
    Guid? DealershipId,
    Guid? ServiceTypeId,
    string? StartTime,
    string? Notes)
{
    /// <summary>Explicit request → command mapping (SPEC §11): it carries the Idempotency-Key header value.</summary>
    public BookAppointmentCommand ToCommand(string? idempotencyKey) =>
        new(CustomerId, VehicleId, DealershipId, ServiceTypeId, StartTime, Notes, idempotencyKey);
}
