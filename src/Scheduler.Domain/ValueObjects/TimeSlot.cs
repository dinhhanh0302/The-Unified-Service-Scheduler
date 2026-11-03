namespace Scheduler.Domain.ValueObjects;

/// <summary>
/// A half-open time window <c>[StartUtc, EndUtc)</c> (BR-01). Back-to-back windows do not overlap.
/// Both bounds are normalised to UTC.
/// </summary>
public readonly record struct TimeSlot
{
    public TimeSlot(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (endUtc <= startUtc)
        {
            throw new ArgumentException("End must be after start.", nameof(endUtc));
        }

        StartUtc = startUtc.ToUniversalTime();
        EndUtc = endUtc.ToUniversalTime();
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    public static TimeSlot FromDuration(DateTimeOffset start, int durationMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);
        return new TimeSlot(start, start.AddMinutes(durationMinutes));
    }

    /// <summary>True when the two half-open windows share at least one instant.</summary>
    public bool Overlaps(TimeSlot other) => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Contains(TimeSlot other) => StartUtc <= other.StartUtc && other.EndUtc <= EndUtc;
}
