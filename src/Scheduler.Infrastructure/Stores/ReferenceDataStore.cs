using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Stores;

internal sealed class ReferenceDataStore(SchedulerDbContext db) : IReferenceDataStore
{
    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Vehicle?> GetVehicleAsync(Guid id, CancellationToken cancellationToken) =>
        db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Dealership?> GetDealershipAsync(Guid id, CancellationToken cancellationToken) =>
        db.Dealerships.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<ServiceType?> GetServiceTypeAsync(Guid id, CancellationToken cancellationToken) =>
        db.ServiceTypes.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Technician?> GetTechnicianAsync(Guid id, CancellationToken cancellationToken) =>
        db.Technicians.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<ServiceBay?> GetServiceBayAsync(Guid id, CancellationToken cancellationToken) =>
        db.ServiceBays.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Dealership>> ListDealershipsAsync(CancellationToken cancellationToken) =>
        await db.Dealerships.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceType>> ListServiceTypesAsync(CancellationToken cancellationToken) =>
        await db.ServiceTypes.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListVehiclesByCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        await db.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderBy(v => v.Vin)
            .ToListAsync(cancellationToken);
}
