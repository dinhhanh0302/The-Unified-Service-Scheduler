using System.Reflection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Scheduler.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded, forward-only SQL scripts in file-name order, each exactly once
/// (recorded in <c>schema_migrations</c>). Runs in one transaction under a PostgreSQL advisory lock,
/// so several API instances starting together cannot apply a script twice.
/// </summary>
public sealed class DatabaseMigrator(NpgsqlDataSource dataSource, ILogger<DatabaseMigrator> logger)
{
    private const string ResourcePrefix = "Migrations.";

    // Arbitrary application-wide key for pg_advisory_xact_lock.
    private const long AdvisoryLockKey = 0x5C4E_D11E;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var scripts = LoadScripts();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction, $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id             varchar(200) PRIMARY KEY,
                applied_at_utc timestamptz  NOT NULL DEFAULT now()
            )
            """,
            cancellationToken);

        var applied = await LoadAppliedAsync(connection, transaction, cancellationToken);

        foreach (var (id, sql) in scripts.Where(script => !applied.Contains(script.Id)))
        {
            logger.LogInformation("Applying database migration {MigrationId}", id);
            await ExecuteAsync(connection, transaction, sql, cancellationToken);

            await using var record = new NpgsqlCommand(
                "INSERT INTO schema_migrations (id) VALUES (@id)", connection, transaction);
            record.Parameters.AddWithValue("id", id);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    internal static IReadOnlyList<(string Id, string Sql)> LoadScripts()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => (Id: name[ResourcePrefix.Length..], Sql: ReadResource(assembly, name)))
            .ToArray();
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded migration '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<HashSet<string>> LoadAppliedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT id FROM schema_migrations", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
