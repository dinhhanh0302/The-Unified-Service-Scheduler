namespace Scheduler.Domain.Services;

/// <summary>Earliest and latest allowed start instants derived from "now" (BR-06).</summary>
public readonly record struct BookingWindow(DateTimeOffset EarliestStartUtc, DateTimeOffset LatestStartUtc)
{
    public bool Contains(DateTimeOffset start) => start >= EarliestStartUtc && start <= LatestStartUtc;
}
