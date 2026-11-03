using Npgsql;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.IntegrationTests.Persistence;

/// <summary>
/// Raw-SQL helpers. Every test creates its own bay, technician and vehicle with fresh ids, so tests can
/// share one database without interfering (no clean-up between tests).
/// </summary>
internal sealed class DatabaseTestData(NpgsqlDataSource dataSource)
{
    public static readonly DateTimeOffset Day = new(2026, 11, 10, 0, 0, 0, TimeSpan.FromHours(7));

    public static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    public async Task<Guid> CreateBayAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO service_bays (id, dealership_id, name, bay_type, is_active) VALUES (@id, @d, @name, 1, true)",
            ("id", id), ("d", SeedIds.Dealerships.D1), ("name", $"Test {id:N}"[..30]));
        return id;
    }

    public async Task<Guid> CreateTechnicianAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO technicians (id, dealership_id, full_name, qualifications, is_active) VALUES (@id, @d, 'Test Technician', '{0}', true)",
            ("id", id), ("d", SeedIds.Dealerships.D1));
        return id;
    }

    public async Task<Guid> CreateVehicleAsync()
    {
        var id = Guid.NewGuid();
        var vin = id.ToString("N")[..17].ToUpperInvariant();
        await ExecuteAsync(
            "INSERT INTO vehicles (id, vin, make, model, year, customer_id) VALUES (@id, @vin, 'Test', 'Car', 2024, @c)",
            ("id", id), ("vin", vin), ("c", SeedIds.Customers.An));
        return id;
    }

    public Task InsertAppointmentAsync(
        Guid bayId,
        Guid technicianId,
        Guid vehicleId,
        DateTimeOffset start,
        DateTimeOffset end,
        string status = "Confirmed",
        NpgsqlConnection? connection = null,
        NpgsqlTransaction? transaction = null) =>
        ExecuteAsync(
            """
            INSERT INTO appointments (id, customer_id, vehicle_id, dealership_id, service_type_id, technician_id,
                                      service_bay_id, start_utc, end_utc, status, created_at_utc)
            VALUES (@id, @c, @v, @d, @s, @t, @b, @start, @end, @status, now())
            """,
            connection,
            transaction,
            ("id", Guid.NewGuid()),
            ("c", SeedIds.Customers.An),
            ("v", vehicleId),
            ("d", SeedIds.Dealerships.D1),
            ("s", SeedIds.ServiceTypes.OilChange),
            ("t", technicianId),
            ("b", bayId),
            ("start", start.ToUniversalTime()),
            ("end", end.ToUniversalTime()),
            ("status", status));

    public async Task<long> CountConfirmedAsync(Guid bayId)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM appointments WHERE service_bay_id = @b AND status = 'Confirmed'");
        command.Parameters.AddWithValue("b", bayId);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters) =>
        ExecuteAsync(sql, null, null, parameters);

    private async Task ExecuteAsync(
        string sql,
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection is null
            ? dataSource.CreateCommand(sql)
            : new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
