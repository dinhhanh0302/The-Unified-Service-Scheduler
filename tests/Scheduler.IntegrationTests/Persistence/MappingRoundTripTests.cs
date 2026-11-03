using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Infrastructure.Persistence;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Persistence;

/// <summary>Every entity survives a save / reload through EF Core with all values intact.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Database")]
public sealed class MappingRoundTripTests(SchedulerApiFactory factory)
{
    [Fact]
    public async Task SaveAndReload_AllEntities_ValuesPreserved()
    {
        var dealership = new Dealership(
            Guid.NewGuid(), "Round Trip Motors", "Asia/Ho_Chi_Minh", new TimeOnly(7, 30), new TimeOnly(18, 0),
            [DayOfWeek.Monday, DayOfWeek.Saturday]);
        var bay = new ServiceBay(Guid.NewGuid(), dealership.Id, "Bay RT", BayType.Diagnostic, isActive: true);
        var technician = new Technician(
            Guid.NewGuid(), dealership.Id, "Round Trip Tech", [Qualification.General, Qualification.HighVoltage], isActive: true);
        var serviceType = new ServiceType(
            Guid.NewGuid(), $"RT_{Guid.NewGuid():N}"[..20], "Round trip service", 45, Qualification.HighVoltage, BayType.Diagnostic);
        var customer = new Customer(Guid.NewGuid(), "Round Trip Customer", $"{Guid.NewGuid():N}@example.com", "+84 1");
        var vehicle = new Vehicle(Guid.NewGuid(), Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(), "Make", "Model", 2025, customer.Id);
        // Far in the future so the cancellation rule (start must be after now) never depends on the run date.
        var start = new DateTimeOffset(2030, 1, 7, 9, 15, 0, TimeSpan.FromHours(7));
        var appointment = Appointment.Confirm(
            Guid.NewGuid(), customer.Id, vehicle.Id, dealership.Id, serviceType, new Allocation(bay, technician), start,
            notes: "Round trip", createdAt: DateTimeOffset.UtcNow, idempotencyKey: $"rt-{Guid.NewGuid():N}", requestHash: new string('a', 64));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            db.AddRange(dealership, customer, serviceType);
            await db.SaveChangesAsync();
            db.AddRange(bay, technician, vehicle);
            await db.SaveChangesAsync();
            db.Add(appointment);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

            var loadedDealership = await db.Dealerships.SingleAsync(d => d.Id == dealership.Id);
            loadedDealership.OpenDays.Should().Equal(DayOfWeek.Monday, DayOfWeek.Saturday);
            loadedDealership.OpeningTime.Should().Be(new TimeOnly(7, 30));
            loadedDealership.ToLocal(start).Hour.Should().Be(9, "the time zone must be usable after materialisation");

            var loadedTechnician = await db.Technicians.SingleAsync(t => t.Id == technician.Id);
            loadedTechnician.Qualifications.Should().Equal(Qualification.General, Qualification.HighVoltage);

            var loadedBay = await db.ServiceBays.SingleAsync(b => b.Id == bay.Id);
            loadedBay.BayType.Should().Be(BayType.Diagnostic);

            var loadedVehicle = await db.Vehicles.SingleAsync(v => v.Id == vehicle.Id);
            loadedVehicle.BelongsTo(customer.Id).Should().BeTrue();

            var loaded = await db.Appointments.SingleAsync(a => a.Id == appointment.Id);
            loaded.Status.Should().Be(AppointmentStatus.Confirmed);
            loaded.StartUtc.Should().Be(start);
            loaded.EndUtc.Should().Be(start.AddMinutes(45));
            loaded.TechnicianId.Should().Be(technician.Id);
            loaded.ServiceBayId.Should().Be(bay.Id);
            loaded.Notes.Should().Be("Round trip");
            loaded.IdempotencyKey.Should().Be(appointment.IdempotencyKey);

            loaded.Cancel(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var cancelled = await db.Appointments.SingleAsync(a => a.Id == appointment.Id);

            cancelled.Status.Should().Be(AppointmentStatus.Cancelled);
            cancelled.CancelledAtUtc.Should().NotBeNull();
        }
    }
}
