using Scheduler.Application.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Appointments;

/// <summary>Builds an <see cref="AppointmentDto"/> for a stored appointment by loading what it references.</summary>
internal static class AppointmentDtoLoader
{
    public static async Task<AppointmentDto> LoadAsync(
        IReferenceDataStore referenceData, Appointment appointment, CancellationToken cancellationToken)
    {
        var dealership = await referenceData.GetDealershipAsync(appointment.DealershipId, cancellationToken);
        var vehicle = await referenceData.GetVehicleAsync(appointment.VehicleId, cancellationToken);
        var serviceType = await referenceData.GetServiceTypeAsync(appointment.ServiceTypeId, cancellationToken);
        var technician = await referenceData.GetTechnicianAsync(appointment.TechnicianId, cancellationToken);
        var bay = await referenceData.GetServiceBayAsync(appointment.ServiceBayId, cancellationToken);

        // Foreign keys guarantee these rows exist for a stored appointment.
        return AppointmentDto.From(
            appointment,
            dealership ?? throw MissingReference(nameof(Dealership)),
            vehicle ?? throw MissingReference(nameof(Vehicle)),
            serviceType ?? throw MissingReference(nameof(ServiceType)),
            technician ?? throw MissingReference(nameof(Technician)),
            bay ?? throw MissingReference(nameof(ServiceBay)));
    }

    private static InvalidOperationException MissingReference(string entity) =>
        new($"{entity} referenced by a stored appointment was not found.");
}
