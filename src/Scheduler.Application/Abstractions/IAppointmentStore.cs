using Scheduler.Domain.Entities;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Application.Abstractions;

/// <summary>
/// One allocation attempt: the snapshot it was based on, the appointment it tried to insert (null when no
/// bay/technician was free) and the insert outcome (null when nothing was inserted).
/// </summary>
public sealed record AllocationAttempt(ResourceSnapshot Snapshot, Appointment? Appointment, InsertResult? Result);

/// <summary>Appointment persistence. Implemented in Infrastructure.</summary>
public interface IAppointmentStore
{
    /// <summary>Loads an appointment for reading or for a later <see cref="UpdateAsync"/>.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves changes to an appointment loaded with <see cref="GetByIdAsync"/>. Uses optimistic concurrency
    /// (row version): returns <see cref="UpdateResult.ConcurrencyConflict"/> when another request changed
    /// the row in between.
    /// </summary>
    Task<UpdateResult> UpdateAsync(Appointment appointment, CancellationToken cancellationToken);

    Task<Appointment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Windows of the vehicle's <c>Confirmed</c> appointments overlapping <c>[fromUtc, toUtc)</c>, any dealership.</summary>
    Task<IReadOnlyList<TimeSlot>> GetVehicleBookingsAsync(
        Guid vehicleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>Bays, technicians and <c>Confirmed</c> appointments of one dealership on one local day.</summary>
    Task<ResourceSnapshot> GetDaySnapshotAsync(Dealership dealership, DateOnly localDate, CancellationToken cancellationToken);

    /// <summary>
    /// Runs one allocation attempt serialised per dealership and local day: inside one transaction, takes a
    /// lock for (dealership, day), reads the day snapshot, lets <paramref name="createAppointment"/> choose
    /// the bay and technician, and inserts the result. Bookings for the same dealership-day therefore never
    /// race each other (no deadlocks between the exclusion-constraint indexes); the constraints remain the
    /// final guarantee (SPEC §10).
    /// </summary>
    Task<AllocationAttempt> AllocateAndInsertAsync(
        Dealership dealership,
        DateOnly localDate,
        Func<ResourceSnapshot, Appointment?> createAppointment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a confirmed appointment. Constraint violations come back as results, never as exceptions
    /// (SPEC §10: they must never surface as 500).
    /// </summary>
    Task<InsertResult> InsertAsync(Appointment appointment, CancellationToken cancellationToken);
}
