namespace Scheduler.Domain.Entities;

/// <summary>A service workshop. Owns the opening hours and the time zone they are evaluated in (BR-05).</summary>
public sealed class Dealership
{
    private readonly TimeZoneInfo _timeZone;

    public Dealership(
        Guid id,
        string name,
        string timeZoneId,
        TimeOnly openingTime,
        TimeOnly closingTime,
        IEnumerable<DayOfWeek> openDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentNullException.ThrowIfNull(openDays);
        if (openingTime >= closingTime)
        {
            throw new ArgumentException("Opening time must be before closing time (no overnight hours).", nameof(closingTime));
        }

        Id = id;
        Name = name;
        TimeZoneId = timeZoneId;
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        OpenDays = openDays.Distinct().Order().ToArray();
        // IANA ids (e.g. "Asia/Ho_Chi_Minh") resolve on Windows and Linux in .NET 6+.
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>IANA time zone id, e.g. <c>Asia/Ho_Chi_Minh</c>.</summary>
    public string TimeZoneId { get; }

    public TimeOnly OpeningTime { get; }

    public TimeOnly ClosingTime { get; }

    public IReadOnlyList<DayOfWeek> OpenDays { get; }

    public bool IsOpenOn(DayOfWeek day) => OpenDays.Contains(day);

    /// <summary>The instant expressed with the dealership's local offset.</summary>
    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, _timeZone);

    public DateOnly LocalDateOf(DateTimeOffset instant) => DateOnly.FromDateTime(ToLocal(instant).DateTime);

    /// <summary>True when the instant falls in a local hour that occurs twice (DST fall-back).</summary>
    public bool IsAmbiguousLocalTime(DateTimeOffset instant) => _timeZone.IsAmbiguousTime(instant);

    /// <summary>
    /// Converts a local date and time to UTC. Returns null when the local time does not exist
    /// (DST spring-forward) or is ambiguous (DST fall-back), per BR-05.
    /// </summary>
    public DateTimeOffset? ToUtc(DateOnly localDate, TimeOnly localTime)
    {
        var local = localDate.ToDateTime(localTime, DateTimeKind.Unspecified);
        if (_timeZone.IsInvalidTime(local) || _timeZone.IsAmbiguousTime(local))
        {
            return null;
        }

        return new DateTimeOffset(local, _timeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
