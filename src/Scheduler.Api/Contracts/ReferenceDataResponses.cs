namespace Scheduler.Api.Contracts;

public sealed record DealershipResponse(
    Guid Id,
    string Name,
    string TimeZoneId,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime,
    IReadOnlyList<string> OpenDays);

public sealed record ServiceTypeDetailsResponse(
    Guid Id,
    string Code,
    string Name,
    int DurationMinutes,
    string RequiredQualification,
    string RequiredBayType);

public sealed record CustomerVehicleResponse(Guid Id, string Vin, string Make, string Model, int Year);
