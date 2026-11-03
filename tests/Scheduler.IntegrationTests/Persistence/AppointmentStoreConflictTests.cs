using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Abstractions;
using Scheduler.IntegrationTests.Api;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Persistence;

/// <summary>SPEC §10: each constraint violation maps to its own InsertResult, never an exception.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Database")]
public sealed class AppointmentStoreConflictTests(SchedulerApiFactory factory)
{
    private readonly ApiTestData _data = new(factory);

    private static DateTimeOffset Tue(int hour, int minute = 0) =>
        new(2026, 11, 10, hour, minute, 0, TimeSpan.FromHours(7));

    private async Task<InsertResult> InsertAsync(Scheduler.Domain.Entities.Appointment appointment)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAppointmentStore>()
            .InsertAsync(appointment, CancellationToken.None);
    }

    private async Task<(Scenario Scenario, Guid OtherVehicle)> BookedAtNineAsync()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 2, technicians: 2);
        await _data.BookDirectlyAsync(scenario, scenario.BayIds[0], scenario.TechnicianIds[0], scenario.VehicleId, Tue(9));
        return (scenario, await _data.CreateVehicleAsync(scenario.CustomerId));
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Insert_SameBayOverlapping_ReturnsBayConflict()
    {
        var (s, otherVehicle) = await BookedAtNineAsync();

        var result = await InsertAsync(await _data.NewAppointmentAsync(s, s.BayIds[0], s.TechnicianIds[1], otherVehicle, Tue(9, 30)));

        result.Should().Be(InsertResult.BayConflict);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Insert_SameTechnicianOverlapping_ReturnsTechnicianConflict()
    {
        var (s, otherVehicle) = await BookedAtNineAsync();

        var result = await InsertAsync(await _data.NewAppointmentAsync(s, s.BayIds[1], s.TechnicianIds[0], otherVehicle, Tue(9, 30)));

        result.Should().Be(InsertResult.TechnicianConflict);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task Insert_SameVehicleOverlapping_ReturnsVehicleConflict()
    {
        var (s, _) = await BookedAtNineAsync();

        var result = await InsertAsync(await _data.NewAppointmentAsync(s, s.BayIds[1], s.TechnicianIds[1], s.VehicleId, Tue(9, 30)));

        result.Should().Be(InsertResult.VehicleConflict);
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Insert_DuplicateIdempotencyKey_ReturnsIdempotencyKeyConflict()
    {
        var scenario = await _data.CreateDealershipAsync(liftBays: 2, technicians: 2);
        var key = $"key-{Guid.NewGuid():N}";
        await InsertAsync(await _data.NewAppointmentAsync(
            scenario, scenario.BayIds[0], scenario.TechnicianIds[0], scenario.VehicleId, Tue(9), key));
        var otherVehicle = await _data.CreateVehicleAsync(scenario.CustomerId);

        var result = await InsertAsync(await _data.NewAppointmentAsync(
            scenario, scenario.BayIds[1], scenario.TechnicianIds[1], otherVehicle, Tue(13), key));

        result.Should().Be(InsertResult.IdempotencyKeyConflict);
    }

    [Fact]
    public async Task Insert_AfterConflictInSameScope_NextInsertSucceeds()
    {
        var (s, otherVehicle) = await BookedAtNineAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAppointmentStore>();
        var conflicting = await _data.NewAppointmentAsync(s, s.BayIds[0], s.TechnicianIds[1], otherVehicle, Tue(9));
        var valid = await _data.NewAppointmentAsync(s, s.BayIds[1], s.TechnicianIds[1], otherVehicle, Tue(9));

        (await store.InsertAsync(conflicting, CancellationToken.None)).Should().Be(InsertResult.BayConflict);
        (await store.InsertAsync(valid, CancellationToken.None)).Should().Be(InsertResult.Success);
    }
}
