namespace Scheduler.Api.Contracts;

public sealed record VehicleResponse(Guid Id, string Vin);

public sealed record ServiceTypeResponse(Guid Id, string Code, int DurationMinutes);

public sealed record TechnicianResponse(Guid Id, string FullName);

public sealed record ServiceBayResponse(Guid Id, string Name);

/// <summary>Appointment as returned by the API (SPEC §8). Times carry the dealership's UTC offset.</summary>
public sealed record AppointmentResponse(
    Guid Id,
    string Status,
    Guid CustomerId,
    VehicleResponse Vehicle,
    Guid DealershipId,
    ServiceTypeResponse ServiceType,
    TechnicianResponse Technician,
    ServiceBayResponse ServiceBay,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CancelledAtUtc);
