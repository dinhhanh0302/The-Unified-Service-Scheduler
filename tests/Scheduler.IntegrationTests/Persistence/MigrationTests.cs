using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;
using Scheduler.IntegrationTests.Infrastructure;

namespace Scheduler.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
[Trait("Category", "Database")]
public sealed class MigrationTests(SchedulerApiFactory factory)
{
    private readonly NpgsqlDataSource _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

    [Fact]
    public async Task Startup_AppliesEveryEmbeddedScript()
    {
        var expected = DatabaseMigrator.LoadScripts().Select(s => s.Id).ToArray();

        var applied = await AppliedMigrationsAsync();

        expected.Should().NotBeEmpty();
        applied.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Migrate_RunAgain_IsIdempotent()
    {
        var before = await AppliedMigrationsAsync();
        var migrator = factory.Services.GetRequiredService<DatabaseMigrator>();

        await migrator.Invoking(m => m.MigrateAsync()).Should().NotThrowAsync();

        (await AppliedMigrationsAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Seed_DealershipD1_MatchesSpecTestContext()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        var d1 = await db.Dealerships.SingleAsync(d => d.Id == SeedIds.Dealerships.D1);
        var oilChange = await db.ServiceTypes.SingleAsync(s => s.Code == "OIL_CHANGE");

        d1.TimeZoneId.Should().Be("Asia/Ho_Chi_Minh");
        d1.OpeningTime.Should().Be(new TimeOnly(8, 0));
        d1.ClosingTime.Should().Be(new TimeOnly(17, 0));
        d1.OpenDays.Should().BeEquivalentTo(new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
        });
        oilChange.DurationMinutes.Should().Be(60);
        oilChange.RequiredQualification.Should().Be(Qualification.General);
        oilChange.RequiredBayType.Should().Be(BayType.Lift);
    }

    private async Task<string[]> AppliedMigrationsAsync()
    {
        var ids = new List<string>();
        await using var command = _dataSource.CreateCommand("SELECT id FROM schema_migrations ORDER BY id");
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return [.. ids];
    }
}
