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

        var result = await BookValidatedAsync(booking, requestHash, cancellationToken);

        // A concurrent request with the same key may have committed while this one was running; its
        // booking then blocks this one (vehicle overlap, no free bay). Answer with the original instead.
        if (result.IsFailure && booking.IdempotencyKey is { } retriedKey)
        {
            var replay = await TryReplayAsync(retriedKey, requestHash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        return result;
    }

    private async Task<Result<BookingResult>> BookValidatedAsync(
        ValidatedBooking booking, string requestHash, CancellationToken cancellationToken)
    {
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
            Allocation? allocation = null;

            // Snapshot, allocation and insert run under the store's per-dealership-day lock.
            var outcome = await appointments.AllocateAndInsertAsync(dealership, localDate, daySnapshot =>
            {
                allocation = AvailabilityEngine.Allocate(dealership, serviceType, slot, daySnapshot);

                return allocation is null
                    ? null
                    : Appointment.Confirm(
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
            }, cancellationToken);

            snapshot = outcome.Snapshot;
            if (outcome is not { Appointment: { } appointment, Result: { } inserted }
                || allocation is not { } chosen)
            {
                break;
            }

            switch (inserted)
            {
                case InsertResult.Success:
                    return new BookingResult(
                        AppointmentDto.From(appointment, dealership, vehicle, serviceType, chosen.Technician, chosen.Bay),
                        Replayed: false);

                case InsertResult.VehicleConflict:
                    return DomainErrors.VehicleAlreadyBooked;

                case InsertResult.IdempotencyKeyConflict:
                    // Null-forgiving is safe: this conflict can only occur when a key was sent.
                    return await TryReplayAsync(booking.IdempotencyKey!, requestHash, cancellationToken)
                           ?? throw new InvalidOperationException(
                               "Idempotency key conflict reported, but no appointment holds the key.");

                case InsertResult.BayConflict or InsertResult.TechnicianConflict or InsertResult.Deadlock:
                    continue;

                default:
                    throw new InvalidOperationException($"Unknown insert result {inserted}.");
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

        var dto = await AppointmentDtoLoader.LoadAsync(referenceData, existing, cancellationToken);
        return new BookingResult(dto, Replayed: true);
    }
}
