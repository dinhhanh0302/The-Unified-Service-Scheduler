using Scheduler.Application.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.UnitTests.Application.Fakes;

/// <summary>
/// In-memory stores for handler tests. <see cref="ForcedInsertResults"/> lets a test simulate database
/// constraint conflicts on the next insert(s), the way PostgreSQL would report them (SPEC §10).
/// </summary>
internal sealed class InMemoryStores : IReferenceDataStore, IAppointmentStore
{
    public List<Customer> Customers { get; } = [];

    public List<Vehicle> Vehicles { get; } = [];

    public List<Dealership> Dealerships { get; } = [];

    public List<ServiceType> ServiceTypes { get; } = [];

    public List<Technician> Technicians { get; } = [];

    public List<ServiceBay> Bays { get; } = [];

    public List<Appointment> Appointments { get; } = [];

    public Queue<InsertResult> ForcedInsertResults { get; } = new();

    public int InsertCalls { get; private set; }

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));

    public Task<Vehicle?> GetVehicleAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.Id == id));

    public Task<Dealership?> GetDealershipAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Dealerships.FirstOrDefault(d => d.Id == id));

    public Task<ServiceType?> GetServiceTypeAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceTypes.FirstOrDefault(s => s.Id == id));

    public Task<Technician?> GetTechnicianAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Technicians.FirstOrDefault(t => t.Id == id));

    public Task<ServiceBay?> GetServiceBayAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Bays.FirstOrDefault(b => b.Id == id));

    public Task<Appointment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(Appointments.FirstOrDefault(a => a.IdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<TimeSlot>> GetVehicleBookingsAsync(
        Guid vehicleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var range = new TimeSlot(fromUtc, toUtc);
        IReadOnlyList<TimeSlot> slots = Appointments
            .Where(a => a.VehicleId == vehicleId && a.Status == AppointmentStatus.Confirmed && a.Slot.Overlaps(range))
            .Select(a => a.Slot)
            .ToArray();
        return Task.FromResult(slots);
    }

    public Task<ResourceSnapshot> GetDaySnapshotAsync(
        Dealership dealership, DateOnly localDate, CancellationToken cancellationToken)
    {
        var bookings = Appointments
            .Where(a => a.DealershipId == dealership.Id
                        && a.Status == AppointmentStatus.Confirmed
                        && dealership.LocalDateOf(a.StartUtc) == localDate)
            .Select(a => new BookedSlot(a.ServiceBayId, a.TechnicianId, a.Slot));

        return Task.FromResult(new ResourceSnapshot(
            Bays.Where(b => b.DealershipId == dealership.Id),
            Technicians.Where(t => t.DealershipId == dealership.Id),
            bookings));
    }

    public Task<InsertResult> InsertAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        InsertCalls++;

        if (ForcedInsertResults.TryDequeue(out var forced))
        {
            if (forced == InsertResult.IdempotencyKeyConflict)
            {
                // Simulates a concurrent request with the same key that committed first.
                Appointments.Add(appointment);
            }

            return Task.FromResult(forced);
        }

        Appointments.Add(appointment);
        return Task.FromResult(InsertResult.Success);
    }
}
