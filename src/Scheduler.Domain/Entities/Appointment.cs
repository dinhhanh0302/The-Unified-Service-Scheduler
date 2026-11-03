using Scheduler.Domain.Enums;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Entities;

/// <summary>
/// The confirmed record linking customer, vehicle, technician, bay and time window (Keyloop requirement 3).
/// Created only through <see cref="Confirm"/>, which enforces End = Start + Duration (BR-01).
/// </summary>
public sealed class Appointment
{
    public const int MaxNotesLength = 500;
    public const int MaxIdempotencyKeyLength = 100;

    private Appointment(
        Guid id,
        Guid customerId,
        Guid vehicleId,
        Guid dealershipId,
        Guid serviceTypeId,
        Guid technicianId,
        Guid serviceBayId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        AppointmentStatus status,
        string? notes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? cancelledAtUtc,
        string? idempotencyKey,
        string? requestHash)
    {
        Id = id;
        CustomerId = customerId;
        VehicleId = vehicleId;
        DealershipId = dealershipId;
        ServiceTypeId = serviceTypeId;
        TechnicianId = technicianId;
        ServiceBayId = serviceBayId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = status;
        Notes = notes;
        CreatedAtUtc = createdAtUtc;
        CancelledAtUtc = cancelledAtUtc;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
    }

    public Guid Id { get; }

    public Guid CustomerId { get; }

    public Guid VehicleId { get; }

    public Guid DealershipId { get; }

    public Guid ServiceTypeId { get; }

    public Guid TechnicianId { get; }

    public Guid ServiceBayId { get; }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? IdempotencyKey { get; }

    public string? RequestHash { get; }

    public TimeSlot Slot => new(StartUtc, EndUtc);

    public static Appointment Confirm(
        Guid id,
        Guid customerId,
        Guid vehicleId,
        Guid dealershipId,
        ServiceType serviceType,
        Allocation allocation,
        DateTimeOffset start,
        string? notes,
        DateTimeOffset createdAt,
        string? idempotencyKey = null,
        string? requestHash = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(allocation);
        if (notes is { Length: > MaxNotesLength })
        {
            throw new ArgumentException($"Notes must be at most {MaxNotesLength} characters.", nameof(notes));
        }

        if (idempotencyKey is { Length: > MaxIdempotencyKeyLength })
        {
            throw new ArgumentException(
                $"Idempotency key must be at most {MaxIdempotencyKeyLength} characters.", nameof(idempotencyKey));
        }

        var slot = TimeSlot.FromDuration(start, serviceType.DurationMinutes);

        return new Appointment(
            id,
            customerId,
            vehicleId,
            dealershipId,
            serviceType.Id,
            allocation.Technician.Id,
            allocation.Bay.Id,
            slot.StartUtc,
            slot.EndUtc,
            AppointmentStatus.Confirmed,
            notes,
            createdAt.ToUniversalTime(),
            cancelledAtUtc: null,
            idempotencyKey,
            requestHash);
    }

    /// <summary>BR-12: only a confirmed appointment that has not started yet can be cancelled.</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Confirmed || StartUtc <= now)
        {
            return DomainErrors.AppointmentNotCancellable;
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = now.ToUniversalTime();
        return Result.Success();
    }
}
