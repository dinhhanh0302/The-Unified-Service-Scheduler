namespace Scheduler.Application.Abstractions;

/// <summary>Outcome of inserting an appointment, mapped from the database constraint that fired (SPEC §10).</summary>
public enum InsertResult
{
    Success = 0,

    /// <summary>Bay exclusion constraint (23P01): another booking took the bay first. Retry.</summary>
    BayConflict = 1,

    /// <summary>Technician exclusion constraint (23P01): another booking took the technician first. Retry.</summary>
    TechnicianConflict = 2,

    /// <summary>Vehicle exclusion constraint (23P01): the vehicle is already booked. No retry.</summary>
    VehicleConflict = 3,

    /// <summary>Unique index on idempotency_key (23505): a concurrent request with the same key won.</summary>
    IdempotencyKeyConflict = 4,

    /// <summary>Deadlock between conflicting inserts (40P01). Treated like a bay/technician conflict.</summary>
    Deadlock = 5
}
