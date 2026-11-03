namespace Scheduler.Application.Abstractions;

public enum UpdateResult
{
    Success = 0,

    /// <summary>The row changed since it was read (PostgreSQL xmin row version).</summary>
    ConcurrencyConflict = 1
}
