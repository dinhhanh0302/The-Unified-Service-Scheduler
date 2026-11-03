using Scheduler.Application.Abstractions;
using Scheduler.Domain.Results;

namespace Scheduler.Application.Appointments.Get;

/// <summary>FR-04: retrieve an appointment by id.</summary>
public sealed class GetAppointmentHandler(IReferenceDataStore referenceData, IAppointmentStore appointments)
{
    public async Task<Result<AppointmentDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await appointments.GetByIdAsync(id, cancellationToken);
        if (appointment is null)
        {
            return DomainErrors.AppointmentNotFound;
        }

        return await AppointmentDtoLoader.LoadAsync(referenceData, appointment, cancellationToken);
    }
}
