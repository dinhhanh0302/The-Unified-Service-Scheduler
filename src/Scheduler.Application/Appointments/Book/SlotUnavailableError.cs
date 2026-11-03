using Scheduler.Domain.Results;

namespace Scheduler.Application.Appointments.Book;

/// <summary>SLOT_UNAVAILABLE carrying up to three alternative start times on the same day (FR-05).</summary>
public sealed record SlotUnavailableError(IReadOnlyList<DateTimeOffset> SuggestedStartTimes)
    : DomainError(DomainErrors.SlotUnavailable.Code, DomainErrors.SlotUnavailable.Message);
