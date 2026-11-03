using Scheduler.Domain.Entities;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Application.Abstractions;

/// <summary>Appointment persistence. Implemented in Infrastructure.</summary>
public interface IAppointmentStore
{
    Task<Appointment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Windows of the vehicle's <c>Confirmed</c> appointments overlapping <c>[fromUtc, toUtc)</c>, any dealership.</summary>
    Task<IReadOnlyList<TimeSlot>> GetVehicleBookingsAsync(
        Guid vehicleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>Bays, technicians and <c>Confirmed</c> appointments of one dealership on one local day.</summary>
    Task<ResourceSnapshot> GetDaySnapshotAsync(Dealership dealership, DateOnly localDate, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a confirmed appointment. Constraint violations come back as results, never as exceptions
    /// (SPEC §10: they must never surface as 500).
    /// </summary>
    Task<InsertResult> InsertAsync(Appointment appointment, CancellationToken cancellationToken);
}
