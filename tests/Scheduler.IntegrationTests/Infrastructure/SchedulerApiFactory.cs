using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Scheduler.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API against a real PostgreSQL 16 container (SPEC §3: no in-memory provider).
/// One container is shared by all tests in the "api" collection.
/// </summary>
public sealed class SchedulerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>SPEC §7 fixed test context.</summary>
    public static readonly DateTimeOffset FrozenNow = new(2026, 11, 9, 8, 0, 0, TimeSpan.FromHours(7));

    // Testcontainers 4.14+: the image is passed to the constructor (parameterless ctor is obsolete).
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("scheduler")
        .WithUsername("scheduler")
        .WithPassword("scheduler")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync() => await _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Scheduler", ConnectionString);
        builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(FrozenNow)));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SchedulerApiFactory>
{
    public const string Name = "api";
}
