using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scheduler.Infrastructure.Health;
using Scheduler.Infrastructure.Persistence;

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

        services.AddDbContext<SchedulerDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

        // Schema and seed data are applied on start-up (SPEC §3, OQ-11).
        services.AddSingleton<DatabaseMigrator>();
        services.AddHostedService<DatabaseMigrationHostedService>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [HealthTags.Ready]);

        return services;
    }
}
