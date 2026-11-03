namespace Scheduler.Application.Abstractions;

/// <summary>Outcome of inserting an appointment, mapped from the database constraint that fired (SPEC §10).</summary>
public enum InsertResult
{
    Success = 0,

    /// <summary>Bay or technician exclusion constraint (23P01), or a deadlock (40P01): re-read and retry.</summary>
    ResourceConflict = 1,

    /// <summary>Vehicle exclusion constraint (23P01): no retry.</summary>
    VehicleConflict = 2,

    /// <summary>Unique index on idempotency_key (23505): a concurrent request with the same key won.</summary>
    IdempotencyKeyConflict = 3
}
