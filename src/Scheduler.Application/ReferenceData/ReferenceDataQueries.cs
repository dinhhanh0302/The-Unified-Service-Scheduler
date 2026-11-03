using Scheduler.Application.Abstractions;
using Scheduler.Domain.Results;

namespace Scheduler.Application.ReferenceData;

/// <summary>FR-09: read-only reference data a client needs to build a booking request.</summary>
public sealed class ReferenceDataQueries(IReferenceDataStore referenceData)
{
    public async Task<IReadOnlyList<DealershipDto>> ListDealershipsAsync(CancellationToken cancellationToken = default) =>
        (await referenceData.ListDealershipsAsync(cancellationToken))
            .Select(d => new DealershipDto(d.Id, d.Name, d.TimeZoneId, d.OpeningTime, d.ClosingTime, d.OpenDays))
            .ToArray();

    public async Task<IReadOnlyList<ServiceTypeDto>> ListServiceTypesAsync(CancellationToken cancellationToken = default) =>
        (await referenceData.ListServiceTypesAsync(cancellationToken))
            .Select(s => new ServiceTypeDto(s.Id, s.Code, s.Name, s.DurationMinutes, s.RequiredQualification, s.RequiredBayType))
            .ToArray();

    public async Task<Result<IReadOnlyList<VehicleDto>>> ListCustomerVehiclesAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        if (await referenceData.GetCustomerAsync(customerId, cancellationToken) is null)
        {
            return DomainErrors.CustomerNotFound;
        }

        var vehicles = await referenceData.ListVehiclesByCustomerAsync(customerId, cancellationToken);
        return Result<IReadOnlyList<VehicleDto>>.Success(
            vehicles.Select(v => new VehicleDto(v.Id, v.Vin, v.Make, v.Model, v.Year)).ToArray());
    }
}
