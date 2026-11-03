using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;

namespace Scheduler.Application.Appointments;

public sealed record VehicleSummary(Guid Id, string Vin);

public sealed record ServiceTypeSummary(Guid Id, string Code, int DurationMinutes);

public sealed record TechnicianSummary(Guid Id, string FullName);

public sealed record ServiceBaySummary(Guid Id, string Name);

/// <summary>
/// An appointment as returned by the use cases. Start and end are expressed in the dealership's time zone
/// offset (SPEC §8); the conversion happens here, not in the API mapping layer (SPEC §11).
/// </summary>
public sealed record AppointmentDto(
    Guid Id,
    AppointmentStatus Status,
    Guid CustomerId,
    VehicleSummary Vehicle,
    Guid DealershipId,
    ServiceTypeSummary ServiceType,
    TechnicianSummary Technician,
    ServiceBaySummary ServiceBay,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CancelledAtUtc)
{
    public static AppointmentDto From(
        Appointment appointment,
        Dealership dealership,
        Vehicle vehicle,
        ServiceType serviceType,
        Technician technician,
        ServiceBay serviceBay)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(dealership);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(technician);
        ArgumentNullException.ThrowIfNull(serviceBay);

        return new AppointmentDto(
            appointment.Id,
            appointment.Status,
            appointment.CustomerId,
            new VehicleSummary(vehicle.Id, vehicle.Vin),
            appointment.DealershipId,
            new ServiceTypeSummary(serviceType.Id, serviceType.Code, serviceType.DurationMinutes),
            new TechnicianSummary(technician.Id, technician.FullName),
            new ServiceBaySummary(serviceBay.Id, serviceBay.Name),
            dealership.ToLocal(appointment.StartUtc),
            dealership.ToLocal(appointment.EndUtc),
            appointment.Notes,
            appointment.CreatedAtUtc,
            appointment.CancelledAtUtc);
    }
}
