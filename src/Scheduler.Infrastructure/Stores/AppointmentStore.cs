using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scheduler.Application.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Stores;

internal sealed class AppointmentStore(SchedulerDbContext db) : IAppointmentStore
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Appointments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<UpdateResult> UpdateAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return UpdateResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return UpdateResult.ConcurrencyConflict;
        }
    }

    public Task<Appointment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.Appointments.AsNoTracking().SingleOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<TimeSlot>> GetVehicleBookingsAsync(
        Guid vehicleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var from = fromUtc.ToUniversalTime();
        var to = toUtc.ToUniversalTime();

        var windows = await db.Appointments.AsNoTracking()
            .Where(a => a.VehicleId == vehicleId
                        && a.Status == AppointmentStatus.Confirmed
                        && a.StartUtc < to
                        && a.EndUtc > from)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);

        return windows.Select(w => new TimeSlot(w.StartUtc, w.EndUtc)).ToArray();
    }

    /// <summary>
    /// One query per resource type for the whole day (SPEC §10). Appointments are read for a UTC range that
    /// safely covers the local day, then filtered on the dealership's local date.
    /// </summary>
    public async Task<ResourceSnapshot> GetDaySnapshotAsync(
        Dealership dealership, DateOnly localDate, CancellationToken cancellationToken)
    {
        var bays = await db.ServiceBays.AsNoTracking()
            .Where(b => b.DealershipId == dealership.Id)
            .ToListAsync(cancellationToken);

        var technicians = await db.Technicians.AsNoTracking()
            .Where(t => t.DealershipId == dealership.Id)
            .ToListAsync(cancellationToken);

        var dayStartUtc = new DateTimeOffset(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var from = dayStartUtc.AddDays(-1);
        var to = dayStartUtc.AddDays(2);

        var bookings = await db.Appointments.AsNoTracking()
            .Where(a => a.DealershipId == dealership.Id
                        && a.Status == AppointmentStatus.Confirmed
                        && a.StartUtc >= from
                        && a.StartUtc < to)
            .Select(a => new { a.ServiceBayId, a.TechnicianId, a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);

        var sameDay = bookings
            .Where(b => dealership.LocalDateOf(b.StartUtc) == localDate)
            .Select(b => new BookedSlot(b.ServiceBayId, b.TechnicianId, new TimeSlot(b.StartUtc, b.EndUtc)));

        return new ResourceSnapshot(bays, technicians, sameDay);
    }

    public async Task<AllocationAttempt> AllocateAndInsertAsync(
        Dealership dealership,
        DateOnly localDate,
        Func<ResourceSnapshot, Appointment?> createAppointment,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Transaction-scoped advisory lock for (dealership, local day); released on commit / rollback.
        var dealershipKey = LockKey(dealership.Id);
        var dayKey = localDate.DayNumber;
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({dealershipKey}, {dayKey})", cancellationToken);

        var snapshot = await GetDaySnapshotAsync(dealership, localDate, cancellationToken);
        var appointment = createAppointment(snapshot);
        if (appointment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AllocationAttempt(snapshot, null, null);
        }

        var result = await InsertAsync(appointment, cancellationToken);
        if (result == InsertResult.Success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        return new AllocationAttempt(snapshot, appointment, result);
    }

    /// <summary>Folds a dealership id into the 32-bit key space of pg_advisory_xact_lock(int, int).</summary>
    internal static int LockKey(Guid dealershipId)
    {
        Span<byte> bytes = stackalloc byte[16];
        dealershipId.TryWriteBytes(bytes);
        return BitConverter.ToInt32(bytes[..4]) ^ BitConverter.ToInt32(bytes[4..8])
               ^ BitConverter.ToInt32(bytes[8..12]) ^ BitConverter.ToInt32(bytes[12..]);
    }

    /// <summary>
    /// Inserts the appointment. Violations of the constraints the booking flow knows about come back as
    /// <see cref="InsertResult"/> values, identified by constraint name (SPEC §10); they never surface as
    /// exceptions. Any other database error is unexpected and still throws.
    /// </summary>
    public async Task<InsertResult> InsertAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        db.Appointments.Add(appointment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return InsertResult.Success;
        }
        catch (Exception ex) when (FindPostgresException(ex) is { } postgres && Classify(postgres) is { } conflict)
        {
            // Drop the failed entity so the next attempt starts from a clean context.
            db.ChangeTracker.Clear();
            return conflict;
        }
    }

    /// <summary>
    /// EF wraps errors it considers transient (e.g. 40P01 deadlock) in an InvalidOperationException around the
    /// DbUpdateException, so the PostgresException is searched for down the whole inner-exception chain.
    /// </summary>
    internal static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    internal static InsertResult? Classify(PostgresException exception) =>
        (exception.SqlState, exception.ConstraintName) switch
        {
            (PostgresErrorCodes.ExclusionViolation, ConstraintNames.BayNoOverlap) => InsertResult.BayConflict,
            (PostgresErrorCodes.ExclusionViolation, ConstraintNames.TechnicianNoOverlap) => InsertResult.TechnicianConflict,
            (PostgresErrorCodes.ExclusionViolation, ConstraintNames.VehicleNoOverlap) => InsertResult.VehicleConflict,
            (PostgresErrorCodes.UniqueViolation, ConstraintNames.IdempotencyKeyUnique) => InsertResult.IdempotencyKeyConflict,
            (PostgresErrorCodes.DeadlockDetected, _) => InsertResult.Deadlock,
            _ => null
        };
}
