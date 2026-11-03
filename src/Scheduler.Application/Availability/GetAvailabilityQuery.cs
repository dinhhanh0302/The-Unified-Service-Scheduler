namespace Scheduler.Application.Availability;

/// <summary>FR-06 query. Nullable so missing parameters come back as VALIDATION_FAILED field errors.</summary>
public sealed record GetAvailabilityQuery(Guid? DealershipId, Guid? ServiceTypeId, DateOnly? Date);

/// <summary>Available start times on a local date, each carrying the dealership's UTC offset.</summary>
public sealed record AvailabilityDto(DateOnly Date, IReadOnlyList<DateTimeOffset> StartTimes);
