using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scheduler.Infrastructure.Health;

namespace Scheduler.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Scheduler";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Resolved lazily so test hosts (WebApplicationFactory) can override the connection string.
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. " +
                    $"Set ConnectionStrings__{ConnectionStringName}.");
            }

            return NpgsqlDataSource.Create(connectionString);
        });

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [HealthTags.Ready]);

        return services;
    }
}
