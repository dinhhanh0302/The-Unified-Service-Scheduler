using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scheduler.IntegrationTests.Infrastructure;
using static Scheduler.IntegrationTests.Persistence.DatabaseTestData;

namespace Scheduler.IntegrationTests.Persistence;

/// <summary>
/// Proves BR-11 at the database level: the exclusion constraints reject overlapping Confirmed
/// appointments regardless of application code.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Database")]
public sealed class ExclusionConstraintTests(SchedulerApiFactory factory)
{
    private readonly NpgsqlDataSource _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

    private DatabaseTestData Data => new(_dataSource);

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Insert_OverlappingWindowSameBay_RejectedByBayConstraint()
    {
        var bay = await Data.CreateBayAsync();
        await Data.InsertAppointmentAsync(bay, await Data.CreateTechnicianAsync(), await Data.CreateVehicleAsync(), At(9), At(10));

        var act = async () => await Data.InsertAppointmentAsync(
            bay, await Data.CreateTechnicianAsync(), await Data.CreateVehicleAsync(), At(9, 30), At(10, 30));

        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;
        error.SqlState.Should().Be(PostgresErrorCodes.ExclusionViolation);
        error.ConstraintName.Should().Be("ex_appointments_bay_no_overlap");
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Insert_OverlappingWindowSameTechnician_RejectedByTechnicianConstraint()
    {
        var technician = await Data.CreateTechnicianAsync();
        await Data.InsertAppointmentAsync(await Data.CreateBayAsync(), technician, await Data.CreateVehicleAsync(), At(9), At(10));

        var act = async () => await Data.InsertAppointmentAsync(
            await Data.CreateBayAsync(), technician, await Data.CreateVehicleAsync(), At(8, 30), At(9, 30));

        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;
        error.ConstraintName.Should().Be("ex_appointments_technician_no_overlap");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task Insert_OverlappingWindowSameVehicle_RejectedByVehicleConstraint()
    {
        var vehicle = await Data.CreateVehicleAsync();
        await Data.InsertAppointmentAsync(await Data.CreateBayAsync(), await Data.CreateTechnicianAsync(), vehicle, At(9), At(10));

        var act = async () => await Data.InsertAppointmentAsync(
            await Data.CreateBayAsync(), await Data.CreateTechnicianAsync(), vehicle, At(9, 15), At(9, 45));

        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;
        error.ConstraintName.Should().Be("ex_appointments_vehicle_no_overlap");
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public async Task Insert_BackToBackWindowsSameResources_Succeeds()
    {
        var bay = await Data.CreateBayAsync();
        var technician = await Data.CreateTechnicianAsync();
        var vehicle = await Data.CreateVehicleAsync();
        await Data.InsertAppointmentAsync(bay, technician, vehicle, At(8), At(9));

        var act = async () => await Data.InsertAppointmentAsync(bay, technician, vehicle, At(9), At(10));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task Insert_OverlappingCancelledAppointment_DoesNotBlock()
    {
        var bay = await Data.CreateBayAsync();
        var technician = await Data.CreateTechnicianAsync();
        await Data.InsertAppointmentAsync(bay, technician, await Data.CreateVehicleAsync(), At(9), At(10), status: "Cancelled");

        var act = async () => await Data.InsertAppointmentAsync(bay, technician, await Data.CreateVehicleAsync(), At(9), At(10));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Insert_TenConcurrentTransactionsSameBayAndWindow_ExactlyOneCommits()
    {
        var bay = await Data.CreateBayAsync();
        var participants = new List<(Guid Technician, Guid Vehicle)>();
        for (var i = 0; i < 10; i++)
        {
            participants.Add((await Data.CreateTechnicianAsync(), await Data.CreateVehicleAsync()));
        }

        var outcomes = await Task.WhenAll(participants.Select(async p =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await Data.InsertAppointmentAsync(bay, p.Technician, p.Vehicle, At(11), At(12), connection: connection, transaction: transaction);
                await transaction.CommitAsync();
                return "committed";
            }
            catch (PostgresException ex)
            {
                return ex.SqlState;
            }
        }));

        outcomes.Count(o => o == "committed").Should().Be(1);
        outcomes.Count(o => o == PostgresErrorCodes.ExclusionViolation).Should().Be(9);
        (await Data.CountConfirmedAsync(bay)).Should().Be(1);
    }
}
