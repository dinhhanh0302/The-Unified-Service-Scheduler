using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Scheduler.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API against a real PostgreSQL 16 container (SPEC §3: no in-memory provider).
/// One container is shared by all tests in the "api" collection.
/// </summary>
public sealed class SchedulerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SchedulerApiFactory>
{
    public const string Name = "api";
}
