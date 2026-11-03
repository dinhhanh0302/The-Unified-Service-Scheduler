using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Scheduler.Infrastructure.Health;

internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL reachable");
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable", ex);
        }
    }
}
