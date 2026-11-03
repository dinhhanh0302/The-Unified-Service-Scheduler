using Scheduler.Domain.Enums;

namespace Scheduler.Application.ReferenceData;

public sealed record DealershipDto(
    Guid Id,
    string Name,
    string TimeZoneId,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime,
    IReadOnlyList<DayOfWeek> OpenDays);

public sealed record ServiceTypeDto(
    Guid Id,
    string Code,
    string Name,
    int DurationMinutes,
    Qualification RequiredQualification,
    BayType RequiredBayType);

public sealed record VehicleDto(Guid Id, string Vin, string Make, string Model, int Year);
