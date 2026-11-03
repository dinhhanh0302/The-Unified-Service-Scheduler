using Microsoft.Extensions.Hosting;

namespace Scheduler.Infrastructure.Persistence;

/// <summary>
/// Runs the migrator when the host starts, before the API accepts requests. A hosted service (rather than
/// code in Program.cs) also runs under WebApplicationFactory in integration tests.
/// </summary>
internal sealed class DatabaseMigrationHostedService(DatabaseMigrator migrator) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => migrator.MigrateAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
