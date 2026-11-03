using Scheduler.Application.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Application.Appointments.Book;

/// <summary>
/// FR-01 – FR-05, FR-08. Runs the checks in the fixed error evaluation order of SPEC §5, then allocates a
/// bay and technician and inserts. Bay/technician conflicts from the database are retried (SPEC §10).
/// </summary>
public sealed class BookAppointmentHandler(
    IReferenceDataStore referenceData,
    IAppointmentStore appointments,
    TimeProvider timeProvider)
{
    public const int MaxAttempts = 3;

    public async Task<Result<BookingResult>> HandleAsync(
        BookAppointmentCommand command, CancellationToken cancellationToken = default)
    {
        // 1–2. Shape, required fields, offset, alignment.
        var validation = BookAppointmentValidator.Validate(command);
        if (validation.Error is { } validationError)
        {
            return validationError;
        }

        var booking = validation.Value;
        var requestHash = RequestHasher.Hash(booking);

        // 3. Idempotency: replay the original, or reject a reused key with a different body.
        if (booking.IdempotencyKey is { } key)
        {
            var replay = await TryReplayAsync(key, requestHash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        // 4. Referenced entities exist, in the order customer → vehicle → dealership → service type.
        var customer = await referenceData.GetCustomerAsync(booking.CustomerId, cancellationToken);
        if (customer is null)
        {
            return DomainErrors.CustomerNotFound;
        }

        var vehicle = await referenceData.GetVehicleAsync(booking.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return DomainErrors.VehicleNotFound;
        }

        var dealership = await referenceData.GetDealershipAsync(booking.DealershipId, cancellationToken);
        if (dealership is null)
        {
            return DomainErrors.DealershipNotFound;
        }

        var serviceType = await referenceData.GetServiceTypeAsync(booking.ServiceTypeId, cancellationToken);
        if (serviceType is null)
        {
            return DomainErrors.ServiceTypeNotFound;
        }

        // 5. Ownership (BR-08).
        if (!vehicle.BelongsTo(customer.Id))
        {
            return DomainErrors.VehicleNotOwned;
        }

        // 6. Booking horizon (BR-06), then 7. opening hours (BR-05).
        var now = timeProvider.GetUtcNow();
        if (BookingPolicy.CheckHorizon(booking.Start, now).Error is { } horizonError)
        {
            return horizonError;
        }

        var slot = TimeSlot.FromDuration(booking.Start, serviceType.DurationMinutes);
        if (BookingPolicy.CheckOpeningHours(dealership, slot).Error is { } openingHoursError)
        {
            return openingHoursError;
        }

        // 8. Vehicle overlap (BR-09), across all dealerships. The same window also feeds suggestions.
        var vehicleBookings = await appointments.GetVehicleBookingsAsync(
            vehicle.Id, slot.StartUtc.AddDays(-1), slot.EndUtc.AddDays(1), cancellationToken);
        if (vehicleBookings.Any(booked => booked.Overlaps(slot)))
        {
            return DomainErrors.VehicleAlreadyBooked;
        }

        // 9. Allocate and insert; retry when another request takes the chosen bay or technician first.
        var localDate = dealership.LocalDateOf(slot.StartUtc);
        var window = BookingPolicy.GetBookingWindow(now);
        ResourceSnapshot? snapshot = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            snapshot = await appointments.GetDaySnapshotAsync(dealership, localDate, cancellationToken);
            var allocation = AvailabilityEngine.Allocate(dealership, serviceType, slot, snapshot);
            if (allocation is null)
            {
                break;
            }

            var appointment = Appointment.Confirm(
                Guid.NewGuid(),
                customer.Id,
                vehicle.Id,
                dealership.Id,
                serviceType,
                allocation,
                booking.Start,
                booking.Notes,
                now,
                booking.IdempotencyKey,
                requestHash);

            switch (await appointments.InsertAsync(appointment, cancellationToken))
            {
                case InsertResult.Success:
                    return new BookingResult(
                        AppointmentDto.From(appointment, dealership, vehicle, serviceType, allocation.Technician, allocation.Bay),
                        Replayed: false);

                case InsertResult.VehicleConflict:
                    return DomainErrors.VehicleAlreadyBooked;

                case InsertResult.IdempotencyKeyConflict:
                    // Null-forgiving is safe: this conflict can only occur when a key was sent.
                    return await TryReplayAsync(booking.IdempotencyKey!, requestHash, cancellationToken)
                           ?? throw new InvalidOperationException(
                               "Idempotency key conflict reported, but no appointment holds the key.");

                case InsertResult.ResourceConflict:
                    continue;

                default:
                    throw new InvalidOperationException("Unknown insert result.");
            }
        }

        // No allocation possible (or retries exhausted): 409 with alternatives from the latest snapshot.
        snapshot ??= await appointments.GetDaySnapshotAsync(dealership, localDate, cancellationToken);
        var suggestions = AvailabilityEngine.SuggestStartTimes(
            dealership, serviceType, slot, snapshot, vehicleBookings, window);

        return new SlotUnavailableError(suggestions.Select(dealership.ToLocal).ToArray());
    }

    /// <summary>Null when the key is unused; otherwise the replayed result or IDEMPOTENCY_KEY_REUSED.</summary>
    private async Task<Result<BookingResult>?> TryReplayAsync(
        string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var existing = await appointments.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return DomainErrors.IdempotencyKeyReused;
        }

        var dto = await LoadDtoAsync(existing, cancellationToken);
        return new BookingResult(dto, Replayed: true);
    }

    private async Task<AppointmentDto> LoadDtoAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var dealership = await referenceData.GetDealershipAsync(appointment.DealershipId, cancellationToken);
        var vehicle = await referenceData.GetVehicleAsync(appointment.VehicleId, cancellationToken);
        var serviceType = await referenceData.GetServiceTypeAsync(appointment.ServiceTypeId, cancellationToken);
        var technician = await referenceData.GetTechnicianAsync(appointment.TechnicianId, cancellationToken);
        var bay = await referenceData.GetServiceBayAsync(appointment.ServiceBayId, cancellationToken);

        // Foreign keys guarantee these rows exist for a stored appointment.
        return AppointmentDto.From(
            appointment,
            dealership ?? throw MissingReference(nameof(Dealership)),
            vehicle ?? throw MissingReference(nameof(Vehicle)),
            serviceType ?? throw MissingReference(nameof(ServiceType)),
            technician ?? throw MissingReference(nameof(Technician)),
            bay ?? throw MissingReference(nameof(ServiceBay)));
    }

    private static InvalidOperationException MissingReference(string entity) =>
        new($"{entity} referenced by a stored appointment was not found.");
}
