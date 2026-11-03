using Scheduler.Application.Abstractions;
using Scheduler.Domain.Results;

namespace Scheduler.Application.Appointments.Cancel;

/// <summary>
/// FR-07 / BR-12: cancel a confirmed appointment that has not started, releasing its bay and technician
/// (the exclusion constraints only consider Confirmed rows). Two concurrent cancels of the same appointment
/// are resolved with the row version: one succeeds, the other gets APPOINTMENT_NOT_CANCELLABLE.
/// </summary>
public sealed class CancelAppointmentHandler(
    IReferenceDataStore referenceData,
    IAppointmentStore appointments,
    TimeProvider timeProvider)
{
    public async Task<Result<AppointmentDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await appointments.GetByIdAsync(id, cancellationToken);
        if (appointment is null)
        {
            return DomainErrors.AppointmentNotFound;
        }

        if (appointment.Cancel(timeProvider.GetUtcNow()).Error is { } cancelError)
        {
            return cancelError;
        }

        if (await appointments.UpdateAsync(appointment, cancellationToken) == UpdateResult.ConcurrencyConflict)
        {
            return DomainErrors.AppointmentNotCancellable;
        }

        return await AppointmentDtoLoader.LoadAsync(referenceData, appointment, cancellationToken);
    }
}
