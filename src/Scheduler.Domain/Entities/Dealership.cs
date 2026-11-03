namespace Scheduler.Domain.Entities;

/// <summary>A service workshop. Owns the opening hours and the time zone they are evaluated in (BR-05).</summary>
public sealed class Dealership
{
    private TimeZoneInfo? _timeZone;

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
        // IANA ids (e.g. "Asia/Ho_Chi_Minh") resolve on Windows and Linux in .NET 6+. Resolving here
        // validates the id up front.
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    /// <summary>For EF Core materialisation only: values are set from the database row afterwards.</summary>
    private Dealership()
    {
        Name = string.Empty;
        TimeZoneId = string.Empty;
        OpenDays = [];
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>IANA time zone id, e.g. <c>Asia/Ho_Chi_Minh</c>.</summary>
    public string TimeZoneId { get; }

    public TimeOnly OpeningTime { get; }

    public TimeOnly ClosingTime { get; }

    public IReadOnlyList<DayOfWeek> OpenDays { get; }

    private TimeZoneInfo Zone => _timeZone ??= TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public bool IsOpenOn(DayOfWeek day) => OpenDays.Contains(day);

    /// <summary>The instant expressed with the dealership's local offset.</summary>
    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    public DateOnly LocalDateOf(DateTimeOffset instant) => DateOnly.FromDateTime(ToLocal(instant).DateTime);

    /// <summary>True when the instant falls in a local hour that occurs twice (DST fall-back).</summary>
    public bool IsAmbiguousLocalTime(DateTimeOffset instant) => Zone.IsAmbiguousTime(instant);

    /// <summary>
    /// Converts a local date and time to UTC. Returns null when the local time does not exist
    /// (DST spring-forward) or is ambiguous (DST fall-back), per BR-05.
    /// </summary>
    public DateTimeOffset? ToUtc(DateOnly localDate, TimeOnly localTime)
    {
        var local = localDate.ToDateTime(localTime, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
        {
            return null;
        }

        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
