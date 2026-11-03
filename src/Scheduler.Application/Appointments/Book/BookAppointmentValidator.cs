using System.Globalization;
using System.Text.RegularExpressions;
using Scheduler.Application.Common;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Results;
using Scheduler.Domain.Services;

namespace Scheduler.Application.Appointments.Book;

/// <summary>
/// Steps 1–2 of the error evaluation order (SPEC §5):
/// 1. shape and required fields → VALIDATION_FAILED (all field errors at once);
/// 2. start time has an explicit offset and is 15-minute aligned → INVALID_START_TIME.
/// </summary>
public static partial class BookAppointmentValidator
{
    public const string IdempotencyKeyField = "Idempotency-Key";

    public static Result<ValidatedBooking> Validate(BookAppointmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        RequireId(command.CustomerId, "customerId", errors);
        RequireId(command.VehicleId, "vehicleId", errors);
        RequireId(command.DealershipId, "dealershipId", errors);
        RequireId(command.ServiceTypeId, "serviceTypeId", errors);

        var startText = command.StartTime?.Trim() ?? string.Empty;
        DateTimeOffset start = default;
        if (startText.Length == 0)
        {
            errors["startTime"] = ["startTime is required."];
        }
        else if (!DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            errors["startTime"] = ["startTime must be an ISO 8601 date and time, e.g. 2026-11-10T09:00:00+07:00."];
        }

        if (command.Notes is { Length: > Appointment.MaxNotesLength })
        {
            errors["notes"] = [$"notes must be at most {Appointment.MaxNotesLength} characters."];
        }

        if (command.IdempotencyKey is not null
            && (string.IsNullOrWhiteSpace(command.IdempotencyKey)
                || command.IdempotencyKey.Length > Appointment.MaxIdempotencyKeyLength))
        {
            errors[IdempotencyKeyField] =
                [$"{IdempotencyKeyField} must be 1–{Appointment.MaxIdempotencyKeyLength} non-blank characters."];
        }

        if (errors.Count > 0)
        {
            return new ValidationFailedError(errors);
        }

        // Step 2: an explicit offset is mandatory (OQ-09), then 15-minute alignment (BR-07).
        if (!ExplicitOffset().IsMatch(startText))
        {
            return DomainErrors.InvalidStartTime;
        }

        if (BookingPolicy.CheckSlotAlignment(start).Error is { } alignmentError)
        {
            return alignmentError;
        }

        // Ids are known to be present here: missing ones produced field errors above.
        return new ValidatedBooking(
            command.CustomerId.GetValueOrDefault(),
            command.VehicleId.GetValueOrDefault(),
            command.DealershipId.GetValueOrDefault(),
            command.ServiceTypeId.GetValueOrDefault(),
            start,
            command.Notes,
            command.IdempotencyKey);
    }

    private static void RequireId(Guid? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null || value == Guid.Empty)
        {
            errors[field] = [$"{field} is required."];
        }
    }

    /// <summary>Ends with "Z" or "±hh:mm" / "±hhmm" after a time component.</summary>
    [GeneratedRegex(@"T\d{2}:\d{2}.*(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}
