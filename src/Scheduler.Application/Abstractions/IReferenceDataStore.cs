using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions;

/// <summary>Read access to reference data. Implemented in Infrastructure.</summary>
public interface IReferenceDataStore
{
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken);

    Task<Vehicle?> GetVehicleAsync(Guid id, CancellationToken cancellationToken);

    Task<Dealership?> GetDealershipAsync(Guid id, CancellationToken cancellationToken);

    Task<ServiceType?> GetServiceTypeAsync(Guid id, CancellationToken cancellationToken);

    Task<Technician?> GetTechnicianAsync(Guid id, CancellationToken cancellationToken);

    Task<ServiceBay?> GetServiceBayAsync(Guid id, CancellationToken cancellationToken);
}
