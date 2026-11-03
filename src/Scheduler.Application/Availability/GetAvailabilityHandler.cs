using Scheduler.Application.Abstractions;
using Scheduler.Application.Common;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;

namespace Scheduler.Application.Availability;

/// <summary>
/// FR-06: every 15-minute start on a date that fits opening hours, lies in the booking horizon and has a
/// compatible bay and qualified technician free. Resource availability only; no vehicle is involved.
/// </summary>
public sealed class GetAvailabilityHandler(
    IReferenceDataStore referenceData,
    IAppointmentStore appointments,
    TimeProvider timeProvider)
{
    public async Task<Result<AvailabilityDto>> HandleAsync(
        GetAvailabilityQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (query.DealershipId is null || query.DealershipId == Guid.Empty)
        {
            errors["dealershipId"] = ["dealershipId is required."];
        }

        if (query.ServiceTypeId is null || query.ServiceTypeId == Guid.Empty)
        {
            errors["serviceTypeId"] = ["serviceTypeId is required."];
        }

        if (query.Date is null)
        {
            errors["date"] = ["date is required (yyyy-MM-dd)."];
        }

        if (errors.Count > 0)
        {
            return new ValidationFailedError(errors);
        }

        // Values are known to be present here: missing ones produced field errors above.
        var dealership = await referenceData.GetDealershipAsync(query.DealershipId.GetValueOrDefault(), cancellationToken);
        if (dealership is null)
        {
            return DomainErrors.DealershipNotFound;
        }

        var serviceType = await referenceData.GetServiceTypeAsync(query.ServiceTypeId.GetValueOrDefault(), cancellationToken);
        if (serviceType is null)
        {
            return DomainErrors.ServiceTypeNotFound;
        }

        var date = query.Date.GetValueOrDefault();
        var snapshot = await appointments.GetDaySnapshotAsync(dealership, date, cancellationToken);
        var window = BookingPolicy.GetBookingWindow(timeProvider.GetUtcNow());

        var startTimes = AvailabilityEngine.GetAvailableStartTimes(dealership, serviceType, date, snapshot, window)
            .Select(dealership.ToLocal)
            .ToArray();

        return new AvailabilityDto(date, startTimes);
    }
}
