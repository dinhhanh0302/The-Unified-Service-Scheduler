using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Infrastructure.Persistence;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Api;

/// <summary>A dealership built for one test: same rules as D1 in SPEC §7, its own bays and technicians.</summary>
internal sealed record Scenario(
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    IReadOnlyList<Guid> BayIds,
    IReadOnlyList<Guid> TechnicianIds);

/// <summary>
/// Creates isolated data per test so API tests sharing one database never block each other through the
/// exclusion constraints (they all book "Tuesday 09:00").
/// </summary>
internal sealed class ApiTestData(SchedulerApiFactory factory)
{
    public static readonly Guid OilChange = SeedIds.ServiceTypes.OilChange;

    public async Task<Scenario> CreateDealershipAsync(int liftBays = 1, int technicians = 1)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        var dealership = new Dealership(
            Guid.NewGuid(),
            $"Test dealership {Guid.NewGuid():N}",
            "Asia/Ho_Chi_Minh",
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]);
        var customer = NewCustomer();
        db.AddRange(dealership, customer);
        await db.SaveChangesAsync();

        var bays = Enumerable.Range(1, liftBays)
            .Select(i => new ServiceBay(Guid.NewGuid(), dealership.Id, $"Bay {i}", BayType.Lift, isActive: true))
            .ToList();
        var techs = Enumerable.Range(1, technicians)
            .Select(i => new Technician(Guid.NewGuid(), dealership.Id, $"Technician {i}", [Qualification.General], isActive: true))
            .ToList();
        var vehicle = NewVehicle(customer.Id);
        db.AddRange(bays);
        db.AddRange(techs);
        db.Add(vehicle);
        await db.SaveChangesAsync();

        return new Scenario(dealership.Id, customer.Id, vehicle.Id, bays.Select(b => b.Id).ToArray(), techs.Select(t => t.Id).ToArray());
    }

    /// <summary>Adds a bay of any type / state to an existing scenario dealership.</summary>
    public async Task<Guid> AddBayAsync(Scenario scenario, string name, BayType bayType, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var bay = new ServiceBay(Guid.NewGuid(), scenario.DealershipId, name, bayType, isActive);
        db.Add(bay);
        await db.SaveChangesAsync();
        return bay.Id;
    }

    /// <summary>Adds a technician with any qualifications / state to an existing scenario dealership.</summary>
    public async Task<Guid> AddTechnicianAsync(Scenario scenario, Qualification[] qualifications, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var technician = new Technician(Guid.NewGuid(), scenario.DealershipId, "Extra technician", qualifications, isActive);
        db.Add(technician);
        await db.SaveChangesAsync();
        return technician.Id;
    }

    public async Task<Guid> CreateVehicleAsync(Guid customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var vehicle = NewVehicle(customerId);
        db.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle.Id;
    }

    public async Task<(Guid CustomerId, Guid VehicleId)> CreateCustomerWithVehicleAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var customer = NewCustomer();
        db.Add(customer);
        await db.SaveChangesAsync();
        var vehicle = NewVehicle(customer.Id);
        db.Add(vehicle);
        await db.SaveChangesAsync();
        return (customer.Id, vehicle.Id);
    }

    /// <summary>Inserts a confirmed OIL_CHANGE appointment directly, bypassing the API; returns its id.</summary>
    public async Task<Guid> BookDirectlyAsync(Scenario scenario, Guid bayId, Guid technicianId, Guid vehicleId, DateTimeOffset start)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var serviceType = await db.ServiceTypes.AsNoTracking().SingleAsync(s => s.Id == OilChange);
        var bay = await db.ServiceBays.AsNoTracking().SingleAsync(b => b.Id == bayId);
        var technician = await db.Technicians.AsNoTracking().SingleAsync(t => t.Id == technicianId);

        var appointment = Appointment.Confirm(
            Guid.NewGuid(), scenario.CustomerId, vehicleId, scenario.DealershipId, serviceType,
            new Allocation(bay, technician), start, notes: null, createdAt: SchedulerApiFactory.FrozenNow);
        db.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    /// <summary>Builds (does not save) a confirmed OIL_CHANGE appointment for the given resources.</summary>
    public async Task<Appointment> NewAppointmentAsync(
        Scenario scenario, Guid bayId, Guid technicianId, Guid vehicleId, DateTimeOffset start, string? idempotencyKey = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var serviceType = await db.ServiceTypes.AsNoTracking().SingleAsync(s => s.Id == OilChange);
        var bay = await db.ServiceBays.AsNoTracking().SingleAsync(b => b.Id == bayId);
        var technician = await db.Technicians.AsNoTracking().SingleAsync(t => t.Id == technicianId);

        return Appointment.Confirm(
            Guid.NewGuid(), scenario.CustomerId, vehicleId, scenario.DealershipId, serviceType,
            new Allocation(bay, technician), start, notes: null, createdAt: SchedulerApiFactory.FrozenNow,
            idempotencyKey, requestHash: idempotencyKey is null ? null : new string('0', 64));
    }

    public async Task<int> CountConfirmedAsync(Guid dealershipId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        return await db.Appointments.CountAsync(
            a => a.DealershipId == dealershipId && a.Status == AppointmentStatus.Confirmed);
    }

    private static Customer NewCustomer() =>
        new(Guid.NewGuid(), "Test Customer", $"{Guid.NewGuid():N}@example.com", "+84 0");

    private static Vehicle NewVehicle(Guid customerId) =>
        new(Guid.NewGuid(), Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(), "Test", "Car", 2024, customerId);
}
