namespace Scheduler.Domain.Results;

/// <summary>Error catalog (SPEC §8). Codes are part of the public API contract and must not change.</summary>
public static class DomainErrors
{
    public static readonly DomainError ValidationFailed =
        new("VALIDATION_FAILED", "One or more fields are missing or malformed.");

    public static readonly DomainError InvalidStartTime =
        new("INVALID_START_TIME", "Start time must include a UTC offset and align to a 15-minute boundary.");

    public static readonly DomainError CustomerNotFound =
        new("CUSTOMER_NOT_FOUND", "The customer does not exist.");

    public static readonly DomainError VehicleNotFound =
        new("VEHICLE_NOT_FOUND", "The vehicle does not exist.");

    public static readonly DomainError DealershipNotFound =
        new("DEALERSHIP_NOT_FOUND", "The dealership does not exist.");

    public static readonly DomainError ServiceTypeNotFound =
        new("SERVICE_TYPE_NOT_FOUND", "The service type does not exist.");

    public static readonly DomainError AppointmentNotFound =
        new("APPOINTMENT_NOT_FOUND", "The appointment does not exist.");

    public static readonly DomainError SlotUnavailable =
        new("SLOT_UNAVAILABLE", "No compatible service bay and qualified technician are free for the whole window.");

    public static readonly DomainError VehicleAlreadyBooked =
        new("VEHICLE_ALREADY_BOOKED", "The vehicle already has an appointment that overlaps this window.");

    public static readonly DomainError AppointmentNotCancellable =
        new("APPOINTMENT_NOT_CANCELLABLE", "Only confirmed appointments that have not started can be cancelled.");

    public static readonly DomainError OutsideOpeningHours =
        new("OUTSIDE_OPENING_HOURS", "The appointment must fit entirely within the dealership's opening hours.");

    public static readonly DomainError OutsideBookingHorizon =
        new("OUTSIDE_BOOKING_HORIZON", "Start time must be at least 60 minutes and at most 60 days ahead.");

    public static readonly DomainError VehicleNotOwned =
        new("VEHICLE_NOT_OWNED", "The vehicle does not belong to the customer.");

    public static readonly DomainError IdempotencyKeyReused =
        new("IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used with a different request.");
}
